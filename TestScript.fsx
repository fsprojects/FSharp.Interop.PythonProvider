
#r "bin/Debug/Samples.PythonCodeProvider.dll"
#r "Dependencies/pythonnet/Python.Runtime.dll"

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
     
Python.math