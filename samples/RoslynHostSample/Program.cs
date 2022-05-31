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
        private static List<ProjectDescriptor> projects;
        private static RoslynHost _host;

        static void Main(string[] args)
        {
            Console.WriteLine("starting");

            projects = new List<ProjectDescriptor>();

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

            Console.WriteLine("creating workspace/solution");

            RoslynWorkspace ws = _host.CreateWorkspace();
            Solution sol = ws.CurrentSolution;

            _host.AddAnalyzerReferences(ws, ref sol);

            CompilationOptions compilationOptions = _host.CreateCompilationOptions_alt("/tmp/testvalue", true);

            int testCase = 2; // a simple switch to test few different scenarios.

            // TODO how to deal with directory separators?
            // => see Path.DirectorySeparatorChar

            ProjectDescriptor pd;

            if ( testCase == 1 ) {

                // create a single project with 2 separate files.

                pd = new ProjectDescriptor( "singleproject", libPath );
                pd.LibraryFiles.Add("System.Runtime.dll");
                pd.LibraryFiles.Add("System.Console.dll");
                pd.SourceFiles.Add("SampleFiles/ClassA.cs");
                pd.SourceFiles.Add("SampleFiles/ClassB.cs");
                projects.Add( pd );

            } else if ( testCase == 2 ) {

                // create two separate projects with one source file in each,
                // and make the 2nd project use the 1st one using a reference.

                pd = new ProjectDescriptor( "firstproject", libPath );
                pd.LibraryFiles.Add("System.Runtime.dll");
                pd.LibraryFiles.Add("System.Console.dll");
                pd.SourceFiles.Add("SampleFiles/ClassA.cs");
                projects.Add( pd );

                pd = new ProjectDescriptor( "secondproject", libPath );
                pd.LibraryFiles.Add("System.Runtime.dll");
                pd.SourceFiles.Add("SampleFiles/ClassB.cs");
                pd.ProjectReferences.Add("firstproject");
                projects.Add( pd );

            } else {
                Console.WriteLine( "ERROR testCase not implemented: " + testCase );
                return;
            }

            // IMPORTANT! it seems thet the project tree must be build from ground-up.
            // giving all project references as parameters to project-creation step.
            // => trying to add the references afterwards does not work.

            // TODO the projects are not yet sorted based on dependencies.
            // TODO how to detect cyclic dependencies (correctly?).

            foreach ( ProjectDescriptor x in projects ) {

                Console.WriteLine();
                Console.Write("the project \"" + x.Name + "\" contains ");
                Console.Write(x.SourceFiles.Count + " source files and ");
                Console.Write(x.LibraryFiles.Count + " library files.");
                Console.WriteLine();

                List<ProjectReference> projectReferences = new List<ProjectReference>();

                foreach ( string projName in x.ProjectReferences ) {

                    ProjectId pId = null;
                    foreach ( ProjectDescriptor y in projects ) {
                        if ( y.Name != projName ) continue;
                        pId = y.GetProjectId();
                    }

                    if ( pId == null ) {
                        Console.WriteLine( "ERROR project not found: " + projName );
                        return;
                    }

                    Console.WriteLine( "  =>  adding project reference: " + x.Name + " => " + projName );

                    ProjectReference pr = new ProjectReference( pId );
                    projectReferences.Add( pr );
                }

                Console.WriteLine();
                Console.WriteLine("creating the project");

                ProjectId projectId = x.CreateProject(_host, ws, ref sol, compilationOptions, projectReferences);
            }

            // since the Solution and Project objects are immutable, various operations will
            // generate updated objects, and therefore existing objects can become obsoleted.
            // => always keep an up-to-date version of the Solution instance.
            // => avoid keeping Project objects, prefer getting fresh Project objects from Solution (using ProjectId).

            ProjectDescriptor.DumpSolutionContents( sol );

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
