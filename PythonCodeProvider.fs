namespace PythonCodeProvider

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

    // Runtime entry points, called by Linq Expressions
    static member GetPythonProperty(pythonModule:string,pythonValueName:string) : PyObject = 

        try PythonEngine.Initialize() with _ -> ()

        let pythonModuleObj = PythonEngine.ImportModule(pythonModule)
        pythonModuleObj.GetAttr(pythonValueName)

// Runtime methods to be called by "invoker" Linq expr
/// Helpers to find the handles in type provider runtime DLL. 
type internal RuntimeInfo (config : TypeProviderConfig) =
    let runtimeAssembly = System.Reflection.Assembly.LoadFrom(config.RuntimeAssembly)

    member x.RuntimeAssembly = runtimeAssembly

    static member GetPythonPropertyExpr (pythonModule:string, pythonValueName:string) =
        Expr.Call(typeof<RuntimeAPI>.GetMethod("GetPythonProperty"), [ Expr.Value(pythonModule); Expr.Value(pythonValueName)] ) 


// TypeProvider Boiler Plate (must be in namespace not a module)
[<Microsoft.FSharp.Core.CompilerServices.TypeProvider>]
type PythonTypeProvider(config : TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()

    let baseType = typeof<System.Object> // some real type is the baseType for the erased types

    let runtimeInfo = RuntimeInfo(config)
    let rootNamespace = "Python"
    let thisAssembly = runtimeInfo.RuntimeAssembly
    do try PythonEngine.Initialize() with _ -> ()
    let pythonRuntime = Runtime()
    let pythonSysModule = PythonEngine.ImportModule("sys")
    let pythonMathModule = PythonEngine.ImportModule("math")
    let pythonIterToolsModule = PythonEngine.ImportModule("itertools")

    let emitPythonModuleType(pythonModuleName:string) =         
        let pythonModule = PythonEngine.ImportModule(pythonModuleName)
        let keyType   = ProvidedTypeDefinition(thisAssembly, rootNamespace, pythonModuleName, Some baseType)
        let pythonModuleItems     = pythonModule.Dir()
        let valueProperty (pythonModuleItem: PyObject) =
            let ty   = typeof<PyObject > 
            let pythonValueName = pythonModuleItem.ToString()
            let prop = ProvidedProperty(pythonValueName, 
                                        ty,                     
                                        IsStatic=true,
                                        GetterCode=(fun _args -> RuntimeInfo.GetPythonPropertyExpr(pythonModuleName,pythonValueName)))        
            if pythonValueName.Contains("built-in function") then
                prop.AddXmlDoc(pythonModuleItem.ToString() + ": " + pythonModuleItem.GetAttr("__doc__").ToString())
            else
                prop.AddXmlDoc("Read the Python value " + pythonModuleName + "." + pythonValueName)
            //prop.AddXmlDoc(pythonModuleItem.ToString() + ": " + pythonModuleItem.GetAttr("__doc__").ToString())
            prop
        for item in pythonModuleItems do 
            keyType.AddMember (valueProperty item) 
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
               with _ -> 
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
