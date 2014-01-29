(*** hide ***)
#I "../../bin"

(**
Sample
======
*)

#r "Python.Runtime.dll"
#r "FSharp.Interop.PythonProvider.dll"

open FSharp.Interop

//Python.math.sin Python.math.pi
//Python.math.cos Python.math.pi

type Python = PythonProvider<"math, sys, os"> 
Python. math.pi
Python.os.curdir

