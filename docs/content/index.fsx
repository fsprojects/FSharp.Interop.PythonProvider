(*** hide ***)
#I "../../bin"

(**
FSharp.Interop.PythonProvider
=============================

Early experimental F# type provider for Python. 

Uses [Python for .NET](https://github.com/pythonnet/pythonnet) for metadata and interop.

Currently uses python27.dll for execution on Windows (this is determined by Python.NET).

Currently 32-bit only.

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The library can be <a href="https://nuget.org/packages/FSharp.Interop.PythonProvider">installed from NuGet</a>:
      <pre>PM> Install-Package FSharp.Interop.PythonProvider -prerelease</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>


*)


(**

Contributing and copyright
--------------------------

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork 
the project and submit pull requests. If you're adding new public API, please also 
consider adding [samples][content] that can be turned into a documentation. You might
also want to read [library design notes][readme] to understand how it works.

The library is available under the Apache 2.0 license, which allows modification and 
redistribution for both commercial and non-commercial purposes. For more information see the 
[License file][license] in the GitHub repository. 

  [content]: https://github.com/fsprojects/FSharp.Interop.PythonProvider/tree/master/docs/content
  [gh]: https://github.com/fsprojects/FSharp.Interop.PythonProvider
  [issues]: https://github.com/fsprojects/FSharp.Interop.PythonProvider/issues
  [readme]: https://github.com/fsprojects/FSharp.Interop.PythonProvider/blob/master/README.md
  [license]: https://github.com/fsprojects/FSharp.Interop.PythonProvider/blob/master/LICENSE.txt
*)
