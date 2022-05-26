using System;
﻿using System.Collections.Generic;
﻿using System.IO;
using System.Reflection;
﻿using System.Threading;

using RoslynPad.Roslyn; // RoslynHost

using AvaloniaEdit.Document; // TextDocument

using RoslynPad.Editor; // AvalonEditTextContainer

using Microsoft.CodeAnalysis; // SourceCodeKind etc
using Microsoft.CodeAnalysis.CSharp; // LanguageVersion etc

using RoslynPad.Roslyn.Diagnostics; // DiagnosticsUpdatedArgs

namespace RoslynHostSample
{
    public class Program
    {
        //private static List<ProjectDescriptor> projects; // TODO only one project at the moment.
        private static RoslynHost _host;

        static void Main(string[] args)
        {
            Console.WriteLine("starting");

            _host = new RoslynHost(LanguageVersion.CSharp7,
            additionalAssemblies: new[]
            {
                //Assembly.Load("RoslynPad.Roslyn.Windows"),
                //Assembly.Load("RoslynPad.Editor.Windows")

                // read these extra MEF components etc...
                Assembly.Load("RoslynPad.Roslyn.Avalonia"),
                Assembly.Load("RoslynPad.Editor.Avalonia")

            }, RoslynHostReferences.NamespaceDefault.With(assemblyReferences: new[]
            {
                typeof(object).Assembly,

                // TODO are all these relevant?
                // TODO need to add something more here?

                typeof(System.Text.RegularExpressions.Regex).Assembly,
                typeof(System.Linq.Enumerable).Assembly,
            }));

            Console.WriteLine("RoslynHost created");

            string libPath = "/usr/share/dotnet/shared/Microsoft.NETCore.App/6.0.5";
            Console.WriteLine("using libPath: " + libPath);

            ProjectDescriptor pd = new ProjectDescriptor( libPath );

            pd.LibraryFiles.Add("System.Runtime.dll");
            pd.LibraryFiles.Add("System.Console.dll");

            // TODO how to deal with directory separators?
            // => see Path.DirectorySeparatorChar

            pd.SourceFiles.Add("SampleFiles/ClassA.cs");
            pd.SourceFiles.Add("SampleFiles/ClassB.cs");

            Console.Write("the project contains ");
            Console.Write(pd.SourceFiles.Count + " source files and ");
            Console.Write(pd.LibraryFiles.Count + " library files.");
            Console.WriteLine();

            Console.WriteLine("creating workspace/solution");

            RoslynWorkspace ws = _host.CreateWorkspace();
            Solution sol = ws.CurrentSolution;

            Console.WriteLine("creating the project");

            CompilationOptions compilationOptions = _host.CreateCompilationOptions_alt("/tmp/testvalue", true);

            Project project = pd.CreateProject(_host, ws, ref sol, compilationOptions);

            pd.DumpSolutionContents( sol );

            Console.WriteLine();
            Console.WriteLine( "solution setup is now READY => waiting for feedback..." );
            Console.WriteLine();

            Thread.Sleep(60000);

            Console.WriteLine("remove the workspace");

            _host.CloseWorkspace( ws );

            Console.WriteLine("COMPLETED!");
        }
    }
}
