namespace PythonTypeProvider.Server

open Python
open Python.Runtime
open System
open System.Diagnostics
open FSharp.Interop

type ModuleName = string 
type Doc = string option

// Put the stuff that accesses Python in an app domain. The python code
// seems to go scrpaing through all loaded DLLs looking for something, and this process triggers
// exceptions when some dynamic assemblies are seen, e.g. when loaded in Visual Studio. Placing the
// provider in an app domain fixes this.
//
// Eventually the provider should probably be placed in a 32-bit external process, the connection to this process
// could also be done using .NET remoting, so it wouldn't be a big change from here.
type PythonStaticInfoServer() = 
    inherit MarshalByRefObject()

    let init =
        lazy
            PythonEngine.Initialize()  
            // I don't fully understand why this call is needed, but without it no lock-releasing seems to happen
            //let nb = PythonEngine.BeginAllowThreads() 
            //PythonEngine.EndAllowThreads(nb)
            System.AppDomain.CurrentDomain.DomainUnload.Add (fun _ ->  Python.Runtime.PythonEngine.Shutdown())

    let acquire() = 
        init.Force()
        let n = PythonEngine.AcquireLock() 
        { new System.IDisposable with member __.Dispose() = PythonEngine.ReleaseLock(n) }

    let runtime = Python.Runtime.Runtime()

    let (?) (pyObject: PyObject) (name: string) = pyObject.GetAttr name 

    let (|Attr|_|) (name: string) (o: PyObject) = 
        if o.HasAttr( name) 
        then Some( o?name)
        else None

    let isbuiltin = 
        let isbuiltinFunc = 
            lazy 
                use _lock = acquire()
                let builtin = PythonEngine.ImportModule("inspect")
                builtin ? isbuiltin
        fun(f: PyObject) -> isbuiltinFunc.Value.Invoke(f).IsTrue()

    let isfunction = 
        let isFunctionFunc = 
            lazy 
                use _lock = acquire()
                let builtin = PythonEngine.ImportModule("inspect")
                builtin ? isfunction
        fun(f: PyObject) -> isFunctionFunc.Value.Invoke(f).IsTrue()

    let argsFromBuiltinDocs(name: string, doc: string) = 
        let firstLine = doc.Split('\n').[0]
        let leftParent, rightParent = firstLine.IndexOf '(', firstLine.LastIndexOf ')'
        let length = rightParent - leftParent - 1
        //Debug.Assert( length > 0, sprintf "For function %s failed to parse doc string:\n %s" name doc)
        //assert ( length >= 0)
        doc.Substring(leftParent + 1, length).Split(',') |> Array.map (fun x -> x.Trim())

    let argForFunction(func: PyObject) = 
        use inpect = PythonEngine.ImportModule("inspect")
        let args = inpect.InvokeMethod("getargspec", func).GetAttr("args")
        [| for i = 0 to args.Length() - 1 do yield args.[i].ToString() |]

//    // Prevent the app domain from exiting, we keep it around forever.
//    // See http://stackoverflow.com/questions/2410221/appdomain-and-marshalbyrefobject-life-time-how-to-avoid-remotingexception
//    override __.InitializeLifetimeService() = null
    let sysModule = lazy Python.SysModule()

    member x.GetLoadedModulesInfo(workingFolder: string, import: string[]): (ModuleName * Doc)[] = 
        try 
            sysModule.Value.AppendToPath( workingFolder)
            import |> Array.iter (fun x -> 
                PythonEngine.ImportModule x |> ignore
            )
            [| for x in sysModule.Value.Modules() -> x.Name, Some x.Doc |]
        with ex ->
            let serializable = Exception(ex.Message)
            serializable.Data.Add("inner", string ex)
            raise serializable

    member __.GetModuleInfo(pyModuleName): (string * string * string[])[] = 
        let m = sysModule.Value.Modules() |> Array.find(fun x -> x.Name = pyModuleName)
        m.Members
        |> Seq.map (fun (name, x) ->
            if x.HasAttr("__name__")
            then 
                let args = 
                    if x.IsCallable() 
                    then
                        try
                            if x.IsBuiltIn
                            then argsFromBuiltinDocs(x.Name, x.Doc)
                            else argForFunction x
                        with ex ->
                            Debug.WriteLine(sprintf "Cannot parse args from doc for built-in function %s. Doc:\n%s" x.Name x.Doc)
                            [| "params" |]
                    else
                        Array.empty

                name, x.Doc, args
            else
                name, null, Array.empty
        )
        |> Seq.toArray

    member x.GetModuleInfo1(pyModuleName): (string * string * string[])[] = 
        use _lock = acquire()
        use pyModule = PythonEngine.ImportModule(pyModuleName)
        use pyModuleItems = pyModule.Dir()
        [| for pyItem in pyModuleItems do
            use pyItemObj = pyModule.GetAttr( pyItem)
            let doc = 
                match pyItemObj.HasAttr( "__doc__"), lazy pyItemObj.GetAttr( "__doc__") with
                | false, _ -> None
                | true, Lazy x when x.Repr() = "None" -> None
                | true, Lazy attr -> 
                    attr.ToString() |> Some

            let memberName = pyItem.ToString()

            let args = 
                if pyItemObj.IsCallable() 
                then
                    match isbuiltin pyItemObj, doc with
                    | true, Some docString -> argsFromBuiltinDocs(memberName, docString)
                    | true, None -> [| "params" |]
                    | false, _ -> 
                        if isfunction pyItemObj
                        then argForFunction pyItemObj
                        else Array.empty
                else 
                    Array.empty

            let doc = 
                if memberName.Contains("built-in function") then
                    use attr = pyItem.GetAttr("__doc__")
                    attr.ToString()
                else
                    if pyItemObj.HasAttr("__doc__") then
                        use attr = pyItemObj.GetAttr("__doc__")
                        attr.ToString() 
                    else
                        null 
            yield memberName, doc, args |]

module Main = 
    open System.Runtime.Remoting
    open System.Runtime.Remoting.Lifetime
    open System.Runtime.Remoting.Channels

    [<STAThreadAttribute>]
    [<EntryPoint>]
    let main argv = 
        
        let channelName = argv.[0]
        let chan = new Ipc.IpcChannel(channelName) 
        //LifetimeServices.LeaseTime            <- TimeSpan(7,0,0,0) // days,hours,mins,secs 
        //LifetimeServices.LeaseManagerPollTime <- TimeSpan(7,0,0,0)
        //LifetimeServices.RenewOnCallTime      <- TimeSpan(7,0,0,0)
        //LifetimeServices.SponsorshipTimeout   <- TimeSpan(7,0,0,0)
        ChannelServices.RegisterChannel(chan,false)

        let server = new PythonStaticInfoServer()
        let objRef = RemotingServices.Marshal(server,"PythonStaticInfoServer") 
        RemotingConfiguration.RegisterActivatedServiceType(typeof<PythonStaticInfoServer>)

        printfn "%s started." channelName

        let parentPid = channelName.Split('_').[1]
        let parentProcess = Process.GetProcessById(int parentPid)
        parentProcess.WaitForExit()
        0 // return an integer exit code
