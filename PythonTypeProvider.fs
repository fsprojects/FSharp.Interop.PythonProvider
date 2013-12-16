namespace PythonTypeProvider

open Microsoft.Win32
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Python
open Python.Runtime
open Samples.FSharp.ProvidedTypes
open System
open System.Linq.Expressions

[<AutoOpen>]
module Globals =
  let private init =
    lazy
      try PythonEngine.Initialize() with _ -> ()
      System.AppDomain.CurrentDomain.DomainUnload.Add (fun _ ->  PythonEngine.Shutdown())

  let acquire() = 
     init.Force()
     let n = PythonEngine.AcquireLock() 
     { new System.IDisposable with member __.Dispose() = PythonEngine.ReleaseLock(n) }

type public RuntimeAPI () =
    // Runtime entry points, called by Linq Expressions
    static member GetPythonProperty(pythonModule:string,pythonValueName:string) : PyObject =
        use _lock = acquire()
        let pythonModuleObj = PythonEngine.ImportModule(pythonModule)
        pythonModuleObj.GetAttr(pythonValueName)


// Runtime methods to be called by "invoker" Linq expr
/// Helpers to find the handles in type provider runtime DLL. 
type internal RuntimeInfo (config : TypeProviderConfig) =
    let runtimeAssembly = System.Reflection.Assembly.LoadFrom(config.RuntimeAssembly)

    member x.RuntimeAssembly =
      runtimeAssembly

    static member GetPythonPropertyExpr (pythonModule:string, pythonValueName:string) =
        Expr.Call(typeof<RuntimeAPI>.GetMethod("GetPythonProperty"), [ Expr.Value(pythonModule); Expr.Value(pythonValueName)] ) 



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

    let lock = acquire()
    let pythonSysModule = PythonEngine.ImportModule("sys")
    let pythonMathModule = PythonEngine.ImportModule("math")
    let pythonIterToolsModule = PythonEngine.ImportModule("itertools")


    let emitPythonModuleType(pythonModuleName:string) =         
        let pythonModule = PythonEngine.ImportModule(pythonModuleName)
        let t = ProvidedTypeDefinition(thisAssembly, rootNamespace, pythonModuleName, Some baseType)
        t.AddXmlDocDelayed (fun () ->
          use _lock = acquire()
          if pythonModule.HasAttr "__doc__" then
            (pythonModule.GetAttr "__doc__").ToString()
          else
            sprintf "The Python module %s" (pythonModule.ToString()))
        t.AddMembersDelayed (fun () ->
            use _lock = acquire()
            let pythonModuleItems = pythonModule.Dir()
            [ for item in pythonModuleItems do 
                let pythonValueName = item.ToString()
                let prop = ProvidedProperty(pythonValueName, 
                                            typeof<PyObject>,
                                            IsStatic=true,
                                            GetterCode=(fun _args -> RuntimeInfo.GetPythonPropertyExpr(pythonModuleName,pythonValueName)))        
                                            //GetterCode=(fun _args -> <@@ RuntimeAPI.GetPythonProperty(pythonModuleName,pythonValueName) @@>))        

                if pythonValueName.Contains("built-in function") then
                    let doc = item.ToString() + ": " + item.GetAttr("__doc__").ToString()
                    prop.AddXmlDoc(System.Security.SecurityElement.Escape(doc))
                else
                  let itemObj = pythonModule.GetAttr(item)
                  if itemObj.HasAttr("__doc__") then
                    let doc = item.ToString() + ": " + itemObj.GetAttr("__doc__").ToString()
                    prop.AddXmlDoc(System.Security.SecurityElement.Escape(doc))
                  else
                    prop.AddXmlDoc("Read the Python value " + pythonModuleName + "." + pythonValueName)
                //prop.AddXmlDoc(pythonModuleItem.ToString() + ": " + pythonModuleItem.GetAttr("__doc__").ToString())
                yield prop ])
        t 

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
    do lock.Dispose()
                            
[<assembly:TypeProviderAssembly>]
do()
