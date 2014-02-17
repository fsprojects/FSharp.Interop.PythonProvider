namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("PythonTypeProviderProxy")>]
[<assembly: AssemblyProductAttribute("PythonTypeProviderProxy")>]
[<assembly: AssemblyDescriptionAttribute("Early experimental F# type provider for python.")>]
[<assembly: AssemblyVersionAttribute("0.0.2")>]
[<assembly: AssemblyFileVersionAttribute("0.0.2")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.0.2"
