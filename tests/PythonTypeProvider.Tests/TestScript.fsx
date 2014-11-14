#r "../../bin/Python.Runtime.dll"
#r "../../bin/FSharp.Interop.PythonProvider.dll"

open FSharp.Interop
type Python = PythonProvider<"sys,math,io">

Python.sys.path
printfn "Res = %A" Python.math.pi
Python.math.sin(3.0)
//Python.mytest.aMethod(3, 5)
Python.functools.WRAPPER_UPDATES
Python.io.``open``()

Python.UserDict.DictMixin
Python.__builtin__.ArithmeticError
Python.functools.partial
Python.abc.abstractproperty
Python.sys.copyright
Python.operator.abs( Python.math.pi)

//type MyCode = PythonProvider<"test.py">
//MyCode.aMethod()

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
