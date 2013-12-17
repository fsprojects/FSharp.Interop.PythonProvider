namespace PythonTypeProvider

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
open PythonTypeProvider.Server

module PythonStaticInfo = 
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
        let exePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"PythonTypeProviderProxy.exe")
        let psi = ProcessStartInfo(UseShellExecute=false,CreateNoWindow=true,FileName=exePath, Arguments=channelName,WindowStyle=ProcessWindowStyle.Hidden)
        let p = Process.Start(psi)
        System.AppDomain.CurrentDomain.ProcessExit.Add(fun _ -> p.Kill())
        p.WaitForInputIdle() |> ignore
        p.Exited.Add(fun _ -> lastServer <- None)
        let T = Activator.GetObject(typeof<PythonStaticInfoServer>,"ipc://" + channelName + "/PythonStaticInfoServer") 
        let x = T :?> PythonStaticInfoServer
        lastServer <- Some x
        x

    // This is needed, look on stackoverflow for CreateInstanceFromAndUnwrap errors
    AppDomain.CurrentDomain.add_AssemblyResolve(ResolveEventHandler(fun _ args -> 
        if args.Name = typeof<PythonStaticInfoServer>.Assembly.FullName then 
            typeof<PythonStaticInfoServer>.Assembly 
        else 
            null))



module PythonRuntime = 
    open Python.Runtime
    let init =
      lazy
        try PythonEngine.Initialize()  with _ -> ()
        System.AppDomain.CurrentDomain.DomainUnload.Add (fun _ ->  Python.Runtime.PythonEngine.Shutdown())

/// Used at runtime
type public RuntimeAPI () =

    // Runtime entry points, called by Linq Expressions
    static member GetPythonProperty(pyModule:string,pyValueName:string) : Python.Runtime.PyObject =
        PythonRuntime.init.Force()
        let pyModuleObj = Python.Runtime.PythonEngine.ImportModule(pyModule)
        pyModuleObj.GetAttr(pyValueName)


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

    member x.RuntimeAssembly =
      runtimeAssembly

    static member GetPythonPropertyExpr (pyModule:string, pyValueName:string) =
        Expr.Call(typeof<RuntimeAPI>.GetMethod("GetPythonProperty"), [ Expr.Value(pyModule); Expr.Value(pyValueName)] ) 



// TypeProvider Boiler Plate (must be in namespace not a module)
[<Microsoft.FSharp.Core.CompilerServices.TypeProvider>]
type PythonTypeProvider(config : TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()

    let runtimeInfo = RuntimeInfo(config)
    let thisAssembly = runtimeInfo.RuntimeAssembly

    let addPythonModulesAsMembers (t:ProvidedTypeDefinition, pyModuleName, xmlDoc) = 

            t.AddXmlDocDelayed (fun () ->
              match xmlDoc with 
              | Some doc -> doc
              | None -> sprintf "The Python module %s" pyModuleName)
            t.AddMembersDelayed (fun () ->
                [ for pyValueName, pyItemDoc in PythonStaticInfo.GetServer().GetModuleInfo(pyModuleName) do
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

                    yield prop ]
            )

    let rootNamespace1 = "FSharp.Interop.Python"
    let typesAll = 
        [ for (pyModuleName, xmlDoc) in PythonStaticInfo.GetServer().GetLoadedModulesInfo() do
              let t = ProvidedTypeDefinition(thisAssembly, rootNamespace1, pyModuleName, Some typeof<System.Object> )
              addPythonModulesAsMembers (t, pyModuleName, xmlDoc)
              yield t ]

    do this.AddNamespace(rootNamespace1, typesAll)

// Allow references to user scripts. Disabled as the ModuleFromString is relying on having a side effect
// on the server (loading the script using ModuleFromString), but we allow restarts of the server. Instead 
// we should probaly start a new server for each different use of the type provider (loading only the modules 
// indicated), to get more isolation, or have the seerver only play the call to ModuleFromString once.
#if USER_SCRIPTS
    let rootNamespace2 = "FSharp.Interop"
    let typeBeforeStaticParams = 
        let t = ProvidedTypeDefinition(thisAssembly, rootNamespace2, "PythonProvider", Some typeof<System.Object> )
        t.AddXmlDoc("""Experimental: PythonProvider<"script.py">""")
        t.DefineStaticParameters( [ ProvidedStaticParameter("Script",typeof<string>,null) ] , (fun typeName args -> 
            let scriptFile = args.[0] :?> string
            let moduleName = Path.GetFileNameWithoutExtension(scriptFile)
            PythonStaticInfo.Server.ModuleFromString(moduleName,File.ReadAllText(scriptFile))
            //if result = -1 then failwith "error from RunSimpleString - todo, report the error properly"

            let typeWithStaticParams = ProvidedTypeDefinition(thisAssembly, rootNamespace2, typeName, Some typeof<System.Object> )
            addPythonModulesAsMembers (typeWithStaticParams, moduleName, None)
            typeWithStaticParams))
        t
    do this.AddNamespace(rootNamespace2, [typeBeforeStaticParams])
#endif
                            
[<assembly:TypeProviderAssembly>]
do()
