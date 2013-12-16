namespace PythonTypeProvider

open Microsoft.Win32
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Python
open Python.Runtime
open Samples.FSharp.ProvidedTypes
open System
open System.Linq.Expressions

type public RuntimeAPI () =
    static do try PythonEngine.Initialize() with _ -> ()
//    static do System.AppDomain.CurrentDomain.DomainUnload.Add (fun _ ->  PythonEngine.Shutdown())

    // Runtime entry points, called by Linq Expressions
    static member GetPythonProperty(pythonModule:string,pythonValueName:string) : PyObject = 

        try PythonEngine.Initialize() with _ -> ()

        let pythonModuleObj = PythonEngine.ImportModule(pythonModule)
        pythonModuleObj.GetAttr(pythonValueName)

// Runtime methods to be called by "invoker" Linq expr
/// Helpers to find the handles in type provider runtime DLL. 
type internal RuntimeInfo (config : TypeProviderConfig) =
    static do try PythonEngine.Initialize() with _ -> ()
    static do System.AppDomain.CurrentDomain.DomainUnload.Add (fun _ ->  PythonEngine.Shutdown())
    static let pythonRuntime = Runtime()
    let runtimeAssembly = System.Reflection.Assembly.LoadFrom(config.RuntimeAssembly)

    member x.RuntimeAssembly = runtimeAssembly

    static member GetPythonPropertyExpr (pythonModule:string, pythonValueName:string) =
        Expr.Call(typeof<RuntimeAPI>.GetMethod("GetPythonProperty"), [ Expr.Value(pythonModule); Expr.Value(pythonValueName)] ) 

    static member Initialize() = pythonRuntime |> ignore


// TypeProvider Boiler Plate (must be in namespace not a module)
[<Microsoft.FSharp.Core.CompilerServices.TypeProvider>]
type PythonTypeProvider(config : TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()

    //interface System.IDisposable with 
    //    member x.Dispose() -> 
    let baseType = typeof<System.Object> // some real type is the baseType for the erased types

    let runtimeInfo = RuntimeInfo(config)
    let rootNamespace = "Python"
    let thisAssembly = runtimeInfo.RuntimeAssembly
    do RuntimeInfo.Initialize() |> ignore

    let pythonSysModule = PythonEngine.ImportModule("sys")
    let pythonMathModule = PythonEngine.ImportModule("math")
    let pythonIterToolsModule = PythonEngine.ImportModule("itertools")

    let emitPythonModuleType(pythonModuleName:string) =         
        let pythonModule = PythonEngine.ImportModule(pythonModuleName)
        let keyType   = ProvidedTypeDefinition(thisAssembly, rootNamespace, pythonModuleName, Some baseType)
        keyType.AddXmlDocDelayed (fun () -> 
                try 
                  match pythonModule.GetAttr("__doc__") with 
                  | null -> "no documentation available"
                  | attr -> "documentation: " + attr.ToString()
                with err -> sprintf "no documentation available (error: %s)" err.Message)
        keyType.AddMembersDelayed (fun () -> 
            let pythonModuleItems     = pythonModule.Dir()
            [ for item in pythonModuleItems do 
                let ty   = typeof<PyObject> 
                let pythonValueName = item.ToString()
                let prop = ProvidedProperty(pythonValueName, 
                                            ty,                     
                                            IsStatic=true,
                                            GetterCode=(fun _args -> RuntimeInfo.GetPythonPropertyExpr(pythonModuleName,pythonValueName)))        
                if pythonValueName.Contains("built-in function") then
                    prop.AddXmlDoc(item.ToString() + ": " + item.GetAttr("__doc__").ToString())
                else
                    prop.AddXmlDoc("Read the Python value " + pythonModuleName + "." + pythonValueName)
                //prop.AddXmlDoc(pythonModuleItem.ToString() + ": " + pythonModuleItem.GetAttr("__doc__").ToString())
                yield prop ])
        keyType 


    let typesAll = 
      try
        let modules = pythonSysModule.GetAttr("modules") 
        let keys = modules.InvokeMethod("keys")
        [ for i in 0 .. keys.Length() - 1 do 
            let x =
               try 
                  let moduleName = keys.GetItem(i).ToString() 
                  printfn "importing %s" moduleName
                  match moduleName with 
                  | null -> None
                  | s when s.StartsWith "encodings." -> None
                  | _ -> Some (emitPythonModuleType moduleName)
               with e -> 
                  printfn "error: %s" e.Message;
                  None
            match x with 
            | Some m -> yield m
            | None -> () ] 

      with e -> 
          printfn "error: %A" e
          [ ]

    do this.AddNamespace(rootNamespace, typesAll)
                            
[<assembly:TypeProviderAssembly>]
do()
