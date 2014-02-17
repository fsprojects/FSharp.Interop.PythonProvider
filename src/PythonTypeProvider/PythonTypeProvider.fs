namespace FSharp.Interop

open Microsoft.Win32
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Samples.FSharp.ProvidedTypes
open System
open System.IO
open System.Linq.Expressions
open System.Security
open System.Reflection
open System.Diagnostics
open System.Threading
open PythonTypeProvider.Server

//type Platform = x86 = 0 | x64 = 1

module PythonStaticInfo = 

    [<Literal>]
    let pythonDesignTimeProxy = "PythonDesignTimeProxy.exe"

    let mutable lastServer = None
    let GetServer() =
      match lastServer with 
      | Some s -> s
      | None -> 
        // Restart the server
        let channelName = 
            let randomSalt = System.Random() 
            let pid  = System.Diagnostics.Process.GetCurrentProcess().Id
            let tick = System.Environment.TickCount
            let salt = randomSalt.Next()
            sprintf "PythonStaticInfoServer_%d_%d_%d" pid tick salt

        let createdNew = ref false
        use serverStarted = new EventWaitHandle(false, EventResetMode.ManualReset, channelName, createdNew);
        assert !createdNew

        let exePath = Path.Combine(Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location), pythonDesignTimeProxy)
        let startInfo = ProcessStartInfo( UseShellExecute = false, CreateNoWindow = true, FileName=exePath, Arguments = channelName, WindowStyle = ProcessWindowStyle.Hidden)
        let p = Process.Start( startInfo, EnableRaisingEvents = true)

        let success = serverStarted.WaitOne()
        assert success
        p.Exited.Add(fun _ -> lastServer <- None)
        let server = Activator.GetObject(typeof<PythonStaticInfoServer>,"ipc://" + channelName + "/PythonStaticInfoServer") :?> PythonStaticInfoServer 
        lastServer <- Some server
        server

    // This is needed, look on stackoverflow for CreateInstanceFromAndUnwrap errors
    AppDomain.CurrentDomain.add_AssemblyResolve(ResolveEventHandler(fun _ args -> 
        if args.Name = typeof<PythonStaticInfoServer>.Assembly.FullName then 
            typeof<PythonStaticInfoServer>.Assembly 
        else 
            null))

open Python.Runtime

module PythonRuntime = 
    let init =
      lazy
        try PythonEngine.Initialize()  with _ -> ()
        System.AppDomain.CurrentDomain.DomainUnload.Add (fun _ ->  Python.Runtime.PythonEngine.Shutdown())

    let convertToPyObject (x : obj) = 
        match x with
        | null -> failwith "invalid null argument value to python function call"
        | :? PyObject as v -> v
        | :? double as v -> new PyFloat(v) :> PyObject
        | :? float32 as v -> new PyFloat(double v) :> PyObject
        | :? int as v -> new PyInt(v) :> PyObject
        | :? int64 as v -> new PyLong(v) :> PyObject
        | :? string as v -> new PyString(v) :> PyObject
        | _ -> failwith "unknown argument type %A" (box(x).GetType()) 

/// Used at runtime
type RuntimeAPI () =
    
    // Runtime entry points, called by Linq Expressions
    static member GetPythonProperty(pyModule:string,pyValueName:string) : Python.Runtime.PyObject =
        PythonRuntime.init.Force()
        let pyModuleObj = Python.Runtime.PythonEngine.ImportModule(pyModule)
        pyModuleObj.GetAttr(pyValueName)

    static member Call(pyModule: string, name: string, args: obj[]) : Python.Runtime.PyObject =
        PythonRuntime.init.Force()
        let pyModuleObj = Python.Runtime.PythonEngine.ImportModule(pyModule)
        pyModuleObj.GetAttr(name).Invoke([| for a in args -> PythonRuntime.convertToPyObject a |])

// Runtime methods to be called by "invoker" Linq expr
/// Helpers to find the handles in type provider runtime DLL. 
type internal RuntimeInfo (config : TypeProviderConfig) =
    let runtimeAssembly = System.Reflection.Assembly.LoadFrom(config.RuntimeAssembly)
    let pythonRuntimeAssembly = System.Reflection.Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(config.RuntimeAssembly), typeof<Python.Runtime.PyObject>.Assembly.GetName().Name + ".dll"))

    // This is needed to load quotation literals
    do System.AppDomain.CurrentDomain.add_AssemblyResolve(ResolveEventHandler(fun _ args -> 
        if args.Name = runtimeAssembly.FullName then 
            runtimeAssembly 
        elif args.Name = pythonRuntimeAssembly.FullName then 
            pythonRuntimeAssembly 
        else 
            null))

    //add resolution folder to sys.path
    do
        PythonRuntime.init.Force()
        let pySysModule = PythonEngine.ImportModule("sys")
        let sysPath = pySysModule.GetAttr("path")
        sysPath.InvokeMethod("append", new PyString( config.ResolutionFolder)) |> ignore

    member x.RuntimeAssembly =
      runtimeAssembly

//    static member GetPythonPropertyExpr (pyModule:string, pyValueName:string) =
//        Expr.Call(typeof<RuntimeAPI>.GetMethod("GetPythonProperty"), [ Expr.Value(pyModule); Expr.Value(pyValueName)] ) 



// TypeProvider Boiler Plate (must be in namespace not a module)
[<Microsoft.FSharp.Core.CompilerServices.TypeProvider>]
type PythonTypeProvider(config : TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()

    let runtimeInfo = RuntimeInfo(config)
    let nameSpace = this.GetType().Namespace
    let targetAssembly = runtimeInfo.RuntimeAssembly

    let addPythonModulesAsMembers (t:ProvidedTypeDefinition, pyModuleName, xmlDoc) = 

            t.AddXmlDocDelayed (fun () ->
              match xmlDoc with 
              | Some doc -> doc
              | None -> sprintf "The Python module %s" pyModuleName)
            t.AddMembersDelayed (fun () ->
                [ for pyValueName, pyItemDoc, pyArgs in PythonStaticInfo.GetServer().GetModuleInfo(pyModuleName) do
                    
                    let newMember : MemberInfo = 
                        match pyArgs with
                        | Some args when args.Length > 0 -> 
                            let parameters = [for a in args -> ProvidedParameter(a, typeof<obj>)]
                            let method' = ProvidedMethod(pyValueName, parameters, returnType = typeof<Python.Runtime.PyObject>, IsStaticMethod = true)
                            method'.InvokeCode <- fun args ->  
                                let argsArray = Expr.NewArray(typeof<obj>, args)
                                <@@ RuntimeAPI.Call(pyModuleName, pyValueName, %%argsArray) @@>

                            let doc = 
                                match pyItemDoc with 
                                | Some doc -> doc
                                | None -> "The Python value " + pyModuleName + "." + pyValueName
                            method'.AddXmlDoc("<summary>" + SecurityElement.Escape(doc) + "</summary>")
                            upcast method'
                            
                        | _ -> 
                            let prop = ProvidedProperty(pyValueName, 
                                                        typeof<Python.Runtime.PyObject>,
                                                        IsStatic=true,
                                                        //GetterCode=(fun _args -> RuntimeInfo.GetPythonPropertyExpr(pyModuleName,pyValueName)))        
                                                        GetterCode=(fun _args -> <@@ RuntimeAPI.GetPythonProperty(pyModuleName,pyValueName) @@>))        

                            let doc = 
                                match pyItemDoc with 
                                | Some doc -> doc
                                | None -> "The Python value " + pyModuleName + "." + pyValueName
                            prop.AddXmlDoc("<summary>" + SecurityElement.Escape(doc) + "</summary>")
                            upcast prop

                    yield newMember ]
            )

//    let typesAll = 
//        [ for (pyModuleName, xmlDoc) in PythonStaticInfo.GetServer().GetLoadedModulesInfo() do
//              let t = ProvidedTypeDefinition(targetAssembly, nameSpace, pyModuleName, Some typeof<System.Object> )
//              addPythonModulesAsMembers (t, pyModuleName, xmlDoc)
//              yield t ]

    let providerType = ProvidedTypeDefinition(targetAssembly, nameSpace, "PythonProvider", Some typeof<obj>, HideObjectMethods = true)
    
    do 
        providerType.DefineStaticParameters(
            parameters = [ 
                ProvidedStaticParameter("Import", typeof<string>, "") 
            ],             
            instantiationFunction = this.CreateType
        )

        this.AddNamespace( nameSpace, [ providerType])

    member internal this.CreateType typeName parameters = 
        let modulesToImport : string = unbox parameters.[0] 

        let rootType = ProvidedTypeDefinition(targetAssembly, nameSpace, typeName, baseType = Some typeof<obj>, HideObjectMethods = true)

        rootType.AddMembersDelayed <| fun() ->
            [ 
                let server = PythonStaticInfo.GetServer()
                for pyModuleName, xmlDoc in server.GetLoadedModulesInfo(workingFolder = config.ResolutionFolder, import = modulesToImport.Split(','))  do
                    yield this.GetTypeForModule(pyModuleName, xmlDoc)
            ]


        rootType
        
    member internal this.GetTypeForModule(pyModuleName, xmlDoc) = 
        let t = ProvidedTypeDefinition( pyModuleName, Some typeof<System.Object>)
        addPythonModulesAsMembers (t, pyModuleName, xmlDoc)
        t 
        


// Allow references to user scripts. Disabled as the ModuleFromString is relying on having a side effect
// on the server (loading the script using ModuleFromString), but we allow restarts of the server. Instead 
// we should probaly start a new server for each different use of the type provider (loading only the modules 
// indicated), to get more isolation, or have the seerver only play the call to ModuleFromString once.
#if USER_SCRIPTS
    let rootNamespace2 = "FSharp.Interop"
    let typeBeforeStaticParams = 
        let t = ProvidedTypeDefinition(targetAssembly, rootNamespace2, "PythonProvider", Some typeof<System.Object> )
        t.AddXmlDoc("""Experimental: PythonProvider<"script.py">""")
        t.DefineStaticParameters( [ ProvidedStaticParameter("Script",typeof<string>,null) ] , (fun typeName args -> 
            let scriptFile = args.[0] :?> string
            let moduleName = Path.GetFileNameWithoutExtension(scriptFile)
            PythonStaticInfo.Server.ModuleFromString(moduleName,File.ReadAllText(scriptFile))
            //if result = -1 then failwith "error from RunSimpleString - todo, report the error properly"

            let typeWithStaticParams = ProvidedTypeDefinition(targetAssembly, rootNamespace2, typeName, Some typeof<System.Object> )
            addPythonModulesAsMembers (typeWithStaticParams, moduleName, None)
            typeWithStaticParams))
        t
    do this.AddNamespace(rootNamespace2, [typeBeforeStaticParams])
#endif
                            
[<assembly:TypeProviderAssembly>]
do()
