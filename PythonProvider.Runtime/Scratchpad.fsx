#r "../lib/Python.Runtime.dll"
open Python.Runtime
PythonEngine.Initialize()

open System.Collections
open System.Collections.Generic

let invoke action =
    let token = PythonEngine.AcquireLock()
    try action()
    finally PythonEngine.ReleaseLock(token) 

    
let inspect = invoke <| fun() -> PythonEngine.ImportModule("inspect")
let inspect2 = invoke <| fun() -> PythonEngine.ImportModule("inspect")
let math = invoke <| fun() -> PythonEngine.ImportModule("math")
let sysModule = invoke <| fun() -> PythonEngine.ImportModule("sys")
sysModule.GetAttr("__name__").AsManagedObject(typeof<string>) |> unbox<string>
let ms = sysModule.GetAttr("modules")
PyDict.IsDictType ms
let xs' = (new PyDict(ms))
xs'.["encodings.encodings"].IsTrue()

[for x in PyList.AsList(PyDict(sysModule.GetAttr("modules")).Keys()) do let x' = string x in if x'.[0] <> '_' then yield x' ] |> Seq.toArray //|> Seq.groupBy id  |> Seq.filter (fun (x, xs) -> Seq.length xs > 1)

let modules = 
    [
        let keys = ms.GetAttr("keys").Invoke()
        for key in PyList.AsList keys do 
            let key = key.ToString()  
            if key.[0] <> '_'
            then yield key
    ]
    |> List.sort    

let handle = invoke <| fun() -> PythonEngine.ImportModule("inspect")
let isroutine = invoke <| fun() -> handle.GetAttr("isroutine")
let getmembers = invoke <| fun() -> handle.GetAttr("getmembers")
let ismodule = invoke <| fun() -> handle.GetAttr("ismodule")
let getdoc = invoke <| fun() -> handle.GetAttr("getdoc")

ismodule.Invoke(sysModule).IsTrue()
let xs = getmembers.Invoke(sysModule, isroutine) |> PyList.AsList
let x = new PyTuple(xs.[0])
let name = sysModule.GetAttr("__name__")
PyString.IsStringType(name)
name.AsManagedObject(typeof<string>)

PyObject.FromManagedObject(3).GetPythonType()
PyInt(3).GetPythonType()
