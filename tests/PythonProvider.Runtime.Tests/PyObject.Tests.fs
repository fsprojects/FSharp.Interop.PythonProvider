namespace FSharp.Interop

open Xunit
open FSharp.Interop.Python
open Python.Runtime
open Swensen.Unquote

type PyObjectTests() = 
    
    do 
        init.Force()
    let sys = PythonEngine.ImportModule("sys")
    let inspect = PythonEngine.ImportModule("inspect")

    [<Fact>]
    member __.Doc() = 
        let expected = "chr(i) -> character\n\nReturn a string of one character with ordinal i; 0 <= i < 256."
        let chr = PythonEngine.ImportModule("__builtin__").GetAttr("chr")
        Assert.Equal<string>(expected, chr.Doc)

    [<Fact>]
    member __.Name() =
        Assert.Equal<string>("sys", sys.Name)

    [<Fact>]
    member __.IsModule() =
        Assert.True(sys.IsModule)

    [<Fact>]
    member __.GetModules() =
//        let expected = 
//            Array.sort [| "heapq"; "functools"; "random"; "sysconfig"; "struct"; "base64"; "imp"; "collections"; "zipimport"; "string"; "textwrap"; "ssl"; "signal"; "threading"; "token"; "dis"; "cStringIO"; "locale"; "encodings"; "abc"; "re"; "ntpath"; "math"; "optparse"; "UserDict"; "inspect"; "codecs"; "socket"; "thread"; "traceback"; "weakref"; "itertools"; "opcode"; "os"; "operator"; "visualstudio_py_repl"; "select"; "errno"; "binascii"; "sre_constants"; "os.path"; "tokenize"; "encodings.cp1252"; "copy"; "hashlib"; "keyword"; "encodings.aliases"; "exceptions"; "sre_parse"; "copy_reg"; "sre_compile"; "site"; "visualstudio_py_util"; "strop"; "linecache"; "gettext"; "nt"; "genericpath"; "stat"; "warnings"; "encodings.ascii"; "encodings.utf_8"; "sys"; "types"; "time" |] 
        //printfn "\nAll modules:\n%A\n\n" [| for m in SysModule().Modules() -> m.Name |] 
        Assert.NotEmpty [| for m in SysModule().Modules() -> m.Name |]

    [<Fact>]
    member __.GetMembers() =
        let copy = PythonEngine.ImportModule("copy")
        let expected = set ["Error"; "copy"; "deepcopy"]
        let actual = set [ for x in Module(copy).Members -> fst x ]
        test <@ expected = actual @>

//"encodings.encodings", "encodings.__builtin__", "encodings.codecs"