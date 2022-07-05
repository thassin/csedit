# CsEdit

CsEdit is a C# programmer's editor tool, in an early stage of development, based on RoslynPad (https://github.com/roslynpad/roslynpad).

Objectives of CsEdit:

* To support a hierarchy of multiple projects, containing multiple source files (TODO, currently some fixed demo/test projects are used).
* To support Linux, Mono, and multiple (also earlier) C# language versions.

CsEditor features:
* All RoslynPad editor features.
* Go To Definition (an initial implementation exists).
* TODO: Find All References would be nice (not implemented yet).

In it's current form, CsEdit is mainly a test-bench for AvaloniaEdit and Roslyn integration.
The Roslyn projects and source-file documents are created based on fixed definitions.
The default test set contains 2 projects and 3 source files.
There is no UI yet for projects/files, just a button to open new editor windows.

In development is also a real project reader, which can read in projects created using dotnet SDK.

You can select between the demo project and the project reader by calling either Init_demo() or Init() at CsEditWorkspace.cs around line 175.

Compiling
---------

Currently being developed on Linux + dotnetSDKs 6.0 and 3.1:

```
$ cd src/CsEdit.Avalonia
$ dotnet restore
$ dotnet build
$ dotnet run -- ./SampleFiles/testproject03
```

Information about commandline options (related to the project reader):

```
$ dotnet run -- --help
```

