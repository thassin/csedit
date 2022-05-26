using System;
using System.Collections.Generic;
using System.IO;

using RoslynPad.Editor; // AvalonEditTextContainer

using RoslynPad.Roslyn; // RoslynHost
using RoslynPad.Roslyn.Diagnostics; // DiagnosticsUpdatedArgs

using Microsoft.CodeAnalysis; // SourceCodeKind

namespace RoslynHostSample
{
    public class ProjectDescriptor
    {
        public List<string> SourceFiles { get; set; }
        public List<string> LibraryFiles { get; set; }

        private string libraryPath;
        private Dictionary<DocumentId,string> docId_2_filename;

        private Project project;

        public ProjectDescriptor( string libPath )
        {
            libraryPath = libPath;
            docId_2_filename = new Dictionary<DocumentId,string>();

            SourceFiles = new List<string>();
            LibraryFiles = new List<string>();

            project = null;
        }

        public Project CreateProject(RoslynHost host, RoslynWorkspace ws, ref Solution sol, CompilationOptions compilationOptions)
        {
            // create a new project (previous must not exist).
            if ( project != null ) throw new Exception("project already exists!");

            project = host.CreateProject_alt( ws, ref sol, "myproject", SourceCodeKind.Regular, compilationOptions );

            // add all specified libraries and sourcefiles.
            // => it seems that the libraries must be added first.

            foreach ( string libFile in LibraryFiles ) {
                string libPath = libraryPath + "/" + libFile;
                MetadataReference mdr = MetadataReference.CreateFromFile( libPath );
                host.AddMetadataReference_alt( ws, ref sol, ref project, mdr );
            }

            foreach ( string srcFilePath in SourceFiles ) {

                SourceCodeKind sck = SourceCodeKind.Regular;

                // NOTE srcFilePath can be either a plain filename, or a relative path.
                string source = File.ReadAllText( srcFilePath );

                AvaloniaEdit.Document.TextDocument d = new AvaloniaEdit.Document.TextDocument( source );
                AvalonEditTextContainer c = new AvalonEditTextContainer( d );
                DocumentCreationArgs dca = new DocumentCreationArgs( c, "/tmp/xxxx", sck, PrintFeedback );

                DocumentId d_Id = host.AddDocument_alt( ws, ref sol, ref project, dca, srcFilePath );

                docId_2_filename.Add( d_Id, srcFilePath );
            }

            return project;
        }

	public void PrintFeedback( DiagnosticsUpdatedArgs x ) {
            Console.WriteLine( "received diagnostics feedback:" );
            Console.WriteLine( "kind : " + x.Kind.ToString() );
            foreach ( RoslynPad.Roslyn.Diagnostics.DiagnosticData d in x.Diagnostics ) {

                RoslynPad.Roslyn.Diagnostics.DiagnosticDataLocation loc = d.DataLocation;

                // add +1 to line/column information (it starts from zero).
                int line = loc.OriginalStartLine + 1;
                int position = loc.OriginalStartColumn + 1;

                string fileName;
                if ( docId_2_filename.TryGetValue( d.DocumentId, out fileName ) == false ) {
                    fileName = "<unknown>"; // no filename registered for the DocumentId.
                }

                Console.WriteLine( d.Severity.ToString() + " at line " + line + " position " + position + " : " + fileName );
                Console.WriteLine( "  M->    " + d.Message );
                Console.WriteLine( "  T->    " + d.Title ); // sometimes the same as previous?
            }
            Console.WriteLine();
	}

        public void DumpSolutionContents( Solution sol ) {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine( "dump SOLUTION contents:" );
            foreach ( var p in sol.Projects ) {
                Console.WriteLine( "    PROJECT " + p.Name );
                foreach ( var a in p.MetadataReferences ) {
                    Console.WriteLine( "        Assembly " + a.Display );
                }
                foreach ( var d in p.Documents ) {
                    Console.WriteLine( "        DOCUMENT " + d.Name );
                }
            }
            Console.WriteLine();
            Console.WriteLine();
        }
    }
}
