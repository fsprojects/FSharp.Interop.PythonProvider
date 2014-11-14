[<AutoOpen>]
module FSharp.Interop.Python

open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open Python.Runtime

let internal init = 
    lazy 
        PythonEngine.Initialize()
        AppDomain.CurrentDomain.DomainUnload.Add (fun _ ->  PythonEngine.Shutdown())

//let internal getGIL() = 
//    init.Force()
//    let token = PythonEngine.AcquireLock() 
//    { new System.IDisposable with member __.Dispose() = PythonEngine.ReleaseLock(token) }

let invoke action =
    init.Force()
    let token = PythonEngine.AcquireLock()
    try action()
    finally PythonEngine.ReleaseLock(token) 

//let (?) (self: PyObject) (name: string): 'T option = invoke <| fun() -> 
//    if self.HasAttr name
//    then 
//        let attr = self.GetAttr(name)
//        assert(attr.Repr() <> "None")
//        attr.AsManagedObject(typeof<'T>) |> unbox |> Some
//    else
//        None

module Inspect = 
    let internal handle = lazy (invoke <| fun() -> PythonEngine.ImportModule("inspect"))
    let internal isroutine = lazy (invoke <| fun() -> handle.Value.GetAttr("isroutine"))
    let internal ismodule = lazy (invoke <| fun() -> handle.Value.GetAttr("ismodule"))
    let internal getmembers = lazy (invoke <| fun() -> handle.Value.GetAttr("getmembers"))
    let internal getdoc = lazy (invoke <| fun() -> handle.Value.GetAttr("getdoc"))
    let internal isbuiltin = lazy (invoke <| fun() -> handle.Value.GetAttr("isbuiltin"))
    let internal isabstract = lazy (invoke <| fun() -> handle.Value.GetAttr("isabstract"))
    let internal isclass = lazy (invoke <| fun() -> handle.Value.GetAttr("isclass"))

[<Extension>]
type Extensions = 
    [<Extension>]
    static member inline Invoke(this: Lazy<PyObject>, [<ParamArray>] args: PyObject[]) =
        invoke <| fun() -> this.Value.Invoke(args) 

type PyObject with
    //gil required
    member this.Cast<'T>(): 'T = invoke <| fun() -> 
        this.AsManagedObject(typeof<'T>) |> unbox
    member this.TryGetAttr(name: string) = invoke <| fun() -> 
        if this.HasAttr(name)
        then Some( this.GetAttr(name))
        else None
    member this.Name = invoke <| fun() -> 
        assert (this.HasAttr("__name__"))
        this.GetAttr("__name__").Cast<string>()
    member this.Doc: string = invoke <| fun() -> 
        Inspect.getdoc.Invoke(this).Cast()

    member this.IsRoutine = invoke <| fun() -> 
        Inspect.isroutine.Invoke(this).IsTrue() 
    member this.IsModule = invoke <| fun() -> 
        Inspect.ismodule.Invoke(this).IsTrue() 
    member this.IsBuiltIn = invoke <| fun() -> 
        Inspect.isbuiltin.Invoke(this).IsTrue() 
    member this.IsAbstractClass = invoke <| fun() -> 
        Inspect.isabstract.Invoke(this).IsTrue() 
    member this.IsClass = invoke <| fun() -> 
        Inspect.isclass.Invoke(this).IsTrue() 

    member this.IsNotNone = invoke <| fun() -> 
        this.IsTrue()

    member this.IsPublic = this.Name.[0] <> '_'

type Module(handle: PyObject, ?name: string) = 
    
    do assert (handle.IsModule)

    let members = 
        lazy 
            invoke <| fun() ->
                let publicMembers = 
                    match handle.TryGetAttr("__all__") with
                    | Some pyObj -> 
                        seq {
                            for name in pyObj |> PyList.AsList do
                                yield string name, handle.GetAttr(name)
                        }
                    | None -> 
                        seq {
                            for m in Inspect.getmembers.Invoke(handle) |> PyList.AsList do 
                                let name = m.[0].ToString()
                                let isPublic = name.[0] <> '_'
                                if isPublic && not (m.IsModule || m.IsClass)
                                then yield name, m.[1]
                        }

                dict publicMembers
    
    new(name: string) = let handle = invoke <| fun() -> PythonEngine.ImportModule(name) in Module(handle)

    member __.Name = handle.Name
    member __.Doc = handle.Doc

    member this.Item with get key = members.Value.[key] 
    member this.Members = members.Value |> Seq.map (|KeyValue|)

type SysModule() as this = 
    inherit Module("sys")

    let path = lazy (invoke <| fun() -> PyList.AsList this.["path"])
        
    member __.Modules() =             
        invoke <| fun () -> 
            seq { 
                let modules = (new PyDict(this.["modules"])).Items() |> PyList.AsList
                for x in modules do 
                    let ``module`` = x.[1]
                    if ``module``.IsNotNone && (``module``.Name = "__builtin__" || ``module``.IsPublic)
                    then yield Module(``module``)
            }
            |> Seq.distinctBy (fun x -> x.Name)
            |> Seq.toArray
   
    member __.Path = [ for x in path.Value -> string x ]
    member __.AppendToPath(item: string) = path.Value.Append(new PyString( item))

open System.Runtime.CompilerServices
[<assembly:InternalsVisibleTo("PythonProvider.Runtime.Tests")>]
do()
