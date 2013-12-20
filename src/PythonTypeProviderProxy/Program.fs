namespace PythonTypeProvider.Server

// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.
/// Used at compile-time, in a separate app domain
open Python
open Python.Runtime
open System

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

//    // Prevent the app domain from exiting, we keep it around forever.
//    // See http://stackoverflow.com/questions/2410221/appdomain-and-marshalbyrefobject-life-time-how-to-avoid-remotingexception
//    override __.InitializeLifetimeService() = null

    member x.GetLoadedModulesInfo() = 
        use _lock = acquire()
        use pySysModule = PythonEngine.ImportModule("sys")
        use pyMathModule = PythonEngine.ImportModule("math")
        use modules = pySysModule.GetAttr("modules") 
        use keys = modules.InvokeMethod("keys")
        [ for i in 0 .. keys.Length() - 1 do 
            use idx = new PyInt(i)
            use item = keys.[idx]
            let moduleName = item.ToString() 
            match moduleName with 
            | null -> ()
            | s when s.StartsWith "encodings." -> ()
            | _ -> 
                let doc = 
                    use pyModule = PythonEngine.ImportModule(moduleName)
                    if pyModule.HasAttr "__doc__" then
                        use attr = pyModule.GetAttr "__doc__"
                        Some (attr.ToString())
                    else
                        None
                yield moduleName, doc
        ]

(*
    member x.RunSimpleString(code) =  
        use _lock = acquire()
        PythonEngine.RunSimpleString(code)

    member x.ModuleFromString(name, code) =  
        use _lock = acquire()
        try 
            PythonEngine.ModuleFromString(name, code) |> ignore
        with :? PythonException as p -> failwith p.Message
*)

    member x.GetModuleInfo(pyModuleName) = 
        use _lock = acquire()
        use pyModule = PythonEngine.ImportModule(pyModuleName)
        use pyModuleItems = pyModule.Dir()
        [ for pyItem in pyModuleItems do
            let pyValueName = pyItem.ToString()
            let doc = 
                if pyValueName.Contains("built-in function") then
                    use attr = pyItem.GetAttr("__doc__")
                    attr.ToString() |> Some
                else
                    use pyItemObj = pyModule.GetAttr(pyItem)
                    if pyItemObj.HasAttr("__doc__") then
                        use attr = pyItemObj.GetAttr("__doc__")
                        attr.ToString() |> Some
                    else
                        None 
            yield pyValueName, doc ]


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
        //System.Console.WriteLine("Hit <enter> to exit...")
        //let  form = new System.Windows.Forms.Form(Visible=false)
        System.Windows.Forms.Application.Run()
        //form.Visible <- false
        //let line = System.Console.ReadLine()
        0 // return an integer exit code
