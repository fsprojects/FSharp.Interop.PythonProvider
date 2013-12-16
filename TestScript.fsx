#r "Dependencies/pythonnet/Python.Runtime.dll"
#r "bin/Debug/PythonTypeProvider.dll"

// Explicit application needed in the demonstrator
let (%%) (f:Python.Runtime.PyObject) (y: 'T) = 
   let args = 
       match box y with 
       | null -> failwith "invalid null argument value to python function call"
       | :? Python.Runtime.PyObject as v -> v
       | :? double as v -> new Python.Runtime.PyFloat(v) :> Python.Runtime.PyObject
       | :? int as v -> new Python.Runtime.PyInt(v) :> Python.Runtime.PyObject
       | :? int64 as v -> new Python.Runtime.PyLong(v) :> Python.Runtime.PyObject
       | :? string as v -> new Python.Runtime.PyString(v) :> Python.Runtime.PyObject
       | _ -> failwith "unknown argument type %A" (box(y).GetType())
   f.Invoke args
     
Python.math.pi
//Python.math.sin %% 3.0

Python

(*
Python.math.sin %% 3.0

let x = Python.math.sin %% 3.0
(x.AsManagedObject typeof<obj> :?> float)
Python.math.sin.Dir()
x.Dir()

(Python.math.sin.GetAttr "__call__").GetPythonType()
//(Python.math.sin.GetItem "__call__")

let pyString (str: string) =
  new Python.Runtime.PyString(str) :> Python.Runtime.PyObject

Python.math.sin.Invoke[|pyString "func_code"|]
Python.math.sin.GetAttr "func_code"
Python.math.sin.GetItem "func_code" // not subscriptable

Python.math.Dir()

sin 3.0

//open FSharp.Data

//type Freebase.*

//type MyFrebase.* = FreebaseProvider<API_KEY=...>

*)
(*
(Python.math.sin %% 3.0).Dir()
  seq
    [__abs__ {Handle = 168760096n;
              Item = ?;
              Item = ?;
              Item = ?;}; __add__ {Handle = 168759424n;
                                   Item = ?;
                                   Item = ?;
                                   Item = ?;}; __class__ {Handle = 168761856n;
                                                          Item = ?;
                                                          Item = ?;
                                                          Item = ?;};
     __coerce__ {Handle = 168760512n;
                 Item = ?;
                 Item = ?;
                 Item = ?;}; ...]

Python.math.sin.Dir()
  seq
    [__call__ {Handle = 168761184n;
               Item = ?;
               Item = ?;
               Item = ?;}; __class__ {Handle = 168761856n;
                                      Item = ?;
                                      Item = ?;
                                      Item = ?;};
     __cmp__ {Handle = 168761120n;
              Item = ?;
              Item = ?;
              Item = ?;}; __delattr__ {Handle = 168761280n;
                                       Item = ?;
                                       Item = ?;
                                       Item = ?;}; ...]
*)
