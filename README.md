# CsEdit

CsEdit is a C# programmer's editor tool, in an early stage of development, based on RoslynPad (https://github.com/roslynpad/roslynpad).

Objectives of CsEdit:

* To support a hierarchy of multiple projects, containing multiple source files (TODO, currently some fixed demo/test projects are used).
* To support Linux, Mono, and multiple (also earlier) C# language versions.

CsEditor features:
* All RoslynPad editor features.
* Go To Definition (an initial implementation exists).
* TODO: Find All References would be nice (not implemented yet).

In it's current form, CsEdit is mainly a test-bench for AvaloniaEdit and Roslyn integration. The Roslyn projects and source-file documents are created based on fixed definitions. The default test set contains 2 projects and 3 source files. There is no UI yet for projects/files, just a button to open new editor windows.

The next big step would be to implement real project reader, which could read in projects created using dotnet SDK.

