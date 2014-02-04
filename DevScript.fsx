
//#r "Dependencies/pythonnet/Python.Runtime.dll"
#r "bin/Python.Runtime.dll"

open Python
open Python.Runtime

//PythonEngine.InitExt()
PythonEngine.Initialize() 

let pySysModule = PythonEngine.ImportModule("sys")
let sysPath = pySysModule.GetAttr("path")
sysPath.InvokeMethod("append", new PyString(@"C:\Users\mitekm\Documents\GitHub\FSharp.Interop.PythonProvider\tests\PythonTypeProvider.Tests"))
let testModule = PythonEngine.ImportModule("mytest")
let inspectModule = PythonEngine.ImportModule("inspect")
let testFunc = testModule.GetAttr("aMethod")
let argsInfo = inspectModule.InvokeMethod("getargspec", testFunc)
let args2 = argsInfo.GetAttr("args")
testFunc.Invoke(new PyInt(5), new PyInt(7))
PythonEngine.RunSimpleString("5")

PyTuple.IsTupleType(new PyInt(12))

let n = PythonEngine.BeginAllowThreads()
//PythonEngine.EndAllowThreads(n)

let (?) (pyObject: PyObject) (name: string) = pyObject.GetAttr name 

let callable = 
    let builtin = PythonEngine.ImportModule("__builtin__")
    let pythonCallable = builtin ? callable
    fun(f: PyObject) -> pythonCallable.Invoke(f).IsTrue()

let pyBuiltinsModule = PythonEngine.ImportModule("__builtin__")
let import = pyBuiltinsModule.GetAttr("__import__")
let none = PythonEngine.RunSimpleString("eval('None')")
let x = PyObject.FromManagedObject(5.0f)
x.GetPythonType()


let pyMathModule = PythonEngine.ImportModule("math")
let sin = pyMathModule ? sin
let modules = pySysModule.GetAttr("modules") 
let keys = modules.InvokeMethod("keys")


let l = PythonEngine.AcquireLock()
let m = 
    PythonEngine.ModuleFromString("a", """
def aMethod(arg1, arg2):
    pass
    """)
PythonEngine.ReleaseLock l 
let n2 = PythonEngine.BeginAllowThreads()
//let myMethod = PythonEngine.ImportModule("a")



let l = PythonEngine.AcquireLock()
let res = PythonEngine.RunSimpleString("execfile('file.py')")
PythonEngine.ReleaseLock l 


(failwith "fail" : int)

MailboxProcessor<int>.Start(fun inbox -> 
    async { while true do 
              //PythonEngine.Initialize() 
              printfn "locking..."; 
              let l = PythonEngine.AcquireLock()
              printfn "sleeping..., l = %d" l 
              do System.Threading.Thread.Sleep 1000
              printfn "unlocking..."; 
              PythonEngine.ReleaseLock l 
              do System.Threading.Thread.Sleep 1000 })
MailboxProcessor<int>.Start(fun inbox -> 
    async { while true do 
              //PythonEngine.Initialize() 
              printfn "locking..."; 
              let l = PythonEngine.AcquireLock()
              printfn "sleeping..., l = %d" l 
              do System.Threading.Thread.Sleep 1000
              printfn "unlocking..."; 
              PythonEngine.ReleaseLock l 
              do System.Threading.Thread.Sleep 1000 })

MailboxProcessor<int>.Start(fun inbox -> 
    async { while true do 
              //PythonEngine.Initialize() 
              printfn "2 locking..."; 
              let l = PythonEngine.AcquireLock()
              printfn "2 sleeping..., l = %d" l 
              do System.Threading.Thread.Sleep 1000
              printfn "2 unlocking..."; 
              PythonEngine.ReleaseLock l 
              do System.Threading.Thread.Sleep 1000 })

while true do 
    printfn "MAIN: locking..."; 
    let l = PythonEngine.AcquireLock()
    printfn "MAIN: sleeping..., l = %d" l 
    do System.Threading.Thread.Sleep 1000
    printfn "MAIN: releasing..."; 
    PythonEngine.ReleaseLock l
    do System.Threading.Thread.Sleep 1000

1
(*
def describe_builtin(obj):
   """ Describe a builtin function """

   wi('+Built-in Function: %s' % obj.__name__)
   # Built-in functions cannot be inspected by
   # inspect.getargspec. We have to try and parse
   # the __doc__ attribute of the function.
   docstr = obj.__doc__
   args = ''

   if docstr:
      items = docstr.split('\n')
      if items:
         func_descr = items[0]
         s = func_descr.replace(obj.__name__,'')
         idx1 = s.find('(')
         idx2 = s.find(')',idx1)
         if idx1 != -1 and idx2 != -1 and (idx2>idx1+1):
            args = s[idx1+1:idx2]
            wi('\t-Method Arguments:', args)

   if args=='':
      wi('\t-Method Arguments: None')

   print
*)
//let pythonRuntime = Runtime()

let pythonSysModule = PythonEngine.ImportModule("sys")
let pythonMathModule = PythonEngine.ImportModule("math")
let pythonIterToolsModule = PythonEngine.ImportModule("itertools")
let pythonInspectModule = PythonEngine.ImportModule("inspect")

//PythonEngine.RunString("sin")
//PythonEngine.RunString("sin(3.0)")
//PythonEngine.RunString("import math; sin(3.0)")
//PythonEngine.RunString("inspect.getargspec(math.sin)")
//PythonEngine.RunString("inspect.getargspec(aMethod)")

pythonSysModule.GetAttr("__doc__")

let pythonMathModuleItems     = pythonMathModule.Dir()
[ for item in pythonMathModuleItems -> item.ToString() ]

let pythonPiObject = pythonMathModule.GetAttr("pi")
let pythonSinObject = pythonMathModule.GetAttr("sin")

let pythonSinCodeObject = pythonSinObject.GetAttr("func_code")
let pythonSinDocObject = pythonSinObject.GetAttr("__doc__")

let myModule = PythonEngine.ModuleFromString("test2", """
def aMethod(arg1, arg2):
    pass
    
p = 1""")
   
let myMethod = myModule.GetAttr("aMethod")
let myMethod = myModule.GetAttr("p")
myModule.Dir()



let myMethodCode = myMethod.GetAttr("func_code")
myMethodCode.GetAttr("co_argcount")
myMethodCode.GetAttr("co_varnames")
myMethodCode.GetAttr("co_varnames").GetItem(0)
myMethodCode.GetAttr("co_varnames").GetItem(1)
myMethodCode.GetAttr("co_varnames").Length()

pythonMathModule.GetAttr("__doc__").ToString()


let getPythonModuleInfo(pythonModuleName:string) =
    let pythonModule = PythonEngine.ImportModule(pythonModuleName)
    try 
        match pythonModule.GetAttr("__doc__") with 
        | null -> "no documentation available"
        | attr -> "documentation: " + attr.ToString()
    with err -> sprintf "no documentation available (error: %s)" err.Message


let moduleInfo = 
    let modules = pythonSysModule.GetAttr("modules") 
    let keys = modules.InvokeMethod("keys")
    [ for i in 0 .. keys.Length() - 1 do 
        let moduleName = keys.GetItem(i).ToString() 
        match moduleName with 
        //| null -> ()
        //| s when s.StartsWith "encodings." -> ()
        | _ -> yield  moduleName ]
    |> List.filter (fun x -> x = "inspect")
(*
)
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
     
*)


