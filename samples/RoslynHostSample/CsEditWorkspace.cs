using System;
﻿using System.Collections.Generic;
﻿using System.IO;
using System.Reflection;
﻿using System.Threading;

using Microsoft.CodeAnalysis; // SourceCodeKind etc
using Microsoft.CodeAnalysis.CSharp; // LanguageVersion etc
using Microsoft.CodeAnalysis.CSharp.Syntax; // CompilationUnitSyntax
using Microsoft.CodeAnalysis.Text; // SourceTextContainer etc

using AvaloniaEdit.Document; // TextDocument

using RoslynPad.Editor; // AvalonEditTextContainer
using RoslynPad.Roslyn; // RoslynHost
using RoslynPad.Roslyn.Diagnostics; // DiagnosticsUpdatedArgs

namespace RoslynHostSample
{
    public class DocumentInfo {
        public ProjectId ProjId;
        public DocumentId DocId;
        public string FilePathRel;
    }

    public class CsEditWorkspace
    {
        private List<ProjectDescriptor_p> _projects; // TODO should this be a dictionary<projectid,PD>???
        private RoslynHost _host;

        private RoslynWorkspace ws;
        private Solution sol;

        #region singleton

        private static CsEditWorkspace _instance = null;
        private static readonly object _instanceLock = new object();

        public static CsEditWorkspace Instance {
            get {
                lock ( _instanceLock ) {
                    if ( _instance == null ) {
                        _instance = new CsEditWorkspace();
                        _instance.Init_demo();
                    }
                    return _instance;
                }
            }
        }

        private CsEditWorkspace() {
            // NOTICE the method Init_demo() will access the Instance property,
            // so calling it here would cause an infinite loop.
        }

        #endregion // singleton

        private void Init_demo()
        {
            Console.WriteLine("starting");

            _projects = new List<ProjectDescriptor_p>();

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

            ws = _host.CreateWorkspace();
            sol = ws.CurrentSolution;

            _host.AddAnalyzerReferences(ws, ref sol);

            CompilationOptions compilationOptions = _host.CreateCompilationOptions_alt("/tmp/testvalue", true);

            int testCase = 3; // a simple switch to test few different scenarios.

            // TODO how to deal with directory separators?
            // => see Path.DirectorySeparatorChar

            ProjectDescriptor_p pd;

            if ( testCase == 1 ) {

                // create a single project with 2 separate files.

                pd = new ProjectDescriptor_p( "singleproject", libPath );
                pd.LibraryFiles.Add("System.Runtime.dll");
                pd.LibraryFiles.Add("System.Console.dll");
                pd.SourceFiles.Add("SampleFiles/ClassA.cs");
                pd.SourceFiles.Add("SampleFiles/ClassB.cs");
                _projects.Add( pd );

            } else if ( testCase == 2 ) {

                // create two separate projects with one source file in each,
                // and make the 2nd project use the 1st one using a reference.

                pd = new ProjectDescriptor_p( "firstproject", libPath );
                pd.LibraryFiles.Add("System.Runtime.dll");
                pd.LibraryFiles.Add("System.Console.dll");
                pd.SourceFiles.Add("SampleFiles/ClassA.cs");
                _projects.Add( pd );

                pd = new ProjectDescriptor_p( "secondproject", libPath );
                pd.LibraryFiles.Add("System.Runtime.dll");
                pd.SourceFiles.Add("SampleFiles/ClassB.cs");
                pd.ProjectReferences.Add("firstproject");
                _projects.Add( pd );

            } else if ( testCase == 3 ) {

                // create two separate projects with 1 + 2 source files,
                // and make the 2nd project use the 1st one using a reference.

                pd = new ProjectDescriptor_p( "firstproject", libPath );
                pd.LibraryFiles.Add("System.Runtime.dll");
                pd.LibraryFiles.Add("System.Console.dll");
                pd.SourceFiles.Add("SampleFiles/ClassC1.cs");
                pd.SourceFiles.Add("SampleFiles/ClassC2.cs");
                _projects.Add( pd );

                pd = new ProjectDescriptor_p( "secondproject", libPath );
                pd.LibraryFiles.Add("System.Runtime.dll");
                pd.SourceFiles.Add("SampleFiles/ClassD.cs");
                pd.ProjectReferences.Add("firstproject");
                _projects.Add( pd );

            } else {
                Console.WriteLine( "ERROR testCase not implemented: " + testCase );
                return;
            }

            // IMPORTANT! it seems thet the project tree must be built from ground-up.
            // giving all project references as parameters to project-creation step.
            // => trying to add the references afterwards does not work.

            // TODO the projects are not yet sorted based on dependencies.
            // TODO how to detect cyclic dependencies (correctly?).

            foreach ( ProjectDescriptor_p x in _projects ) {

                Console.WriteLine();
                Console.Write("the project \"" + x.Name + "\" contains ");
                Console.Write(x.SourceFiles.Count + " source files and ");
                Console.Write(x.LibraryFiles.Count + " library files.");
                Console.WriteLine();

                List<ProjectReference> projectReferences = new List<ProjectReference>();

                foreach ( string projName in x.ProjectReferences ) {

                    ProjectId pId = null;
                    foreach ( ProjectDescriptor_p y in _projects ) {
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

            DumpSolutionContents( sol );

            int testOperation = 1;

            if ( testOperation == 1 ) {

                // wait for feedback...

                Console.WriteLine();
                Console.WriteLine( "solution setup is now READY => waiting for feedback..." );
                Console.WriteLine();

                Thread.Sleep(60000);

            } else {

                // query semantic info.
                // => we can ask a Compilation representing each of the Projects separately.
		// => then query each Compilation/SyntaxTree separately?

                foreach ( ProjectDescriptor_p x in _projects ) {

                    CSharpCompilation comp = (CSharpCompilation) sol.GetProject( x.GetProjectId() ).GetCompilationAsync().GetAwaiter().GetResult();

// https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/semantic-analysis 

                    int treeCount = 0;
                    foreach ( var tree in comp.SyntaxTrees ) {
                        treeCount++;

                        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

// https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/syntax-analysis
                        Console.WriteLine($"The tree is a {root.Kind()} node.");
                        Console.WriteLine($"The tree has {root.Members.Count} elements in it.");
                        Console.WriteLine($"The tree has {root.Usings.Count} using statements. They are:");
                        foreach (UsingDirectiveSyntax element in root.Usings) Console.WriteLine($"\t{element.Name}");

                        try {

                            int caretPosition = 216;
                            SyntaxToken tok = root.FindToken( caretPosition, false );
                            Console.WriteLine( "token in position " + caretPosition + " is " + tok.GetType() + " : '" + tok.Text + "'" );

                            SemanticModel model = comp.GetSemanticModel(tree);

                            SyntaxNode sn = tok.Parent;
                            if ( sn == null ) {
                                Console.WriteLine( "  =>  NO RESULT..." );
                            } else {
                                SymbolInfo si = model.GetSymbolInfo( sn );
                                Console.WriteLine( "  =>  SymbolInfo FOUND." );

// https://docs.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.isymbol?view=roslyn-dotnet-4.2.0
                                ISymbol s = si.Symbol;
                                if ( s != null ) {
                                    Console.WriteLine( "    =>    Symbol.Name=" + s.Name );
                                    foreach ( var l in s.Locations ) Console.WriteLine( "    =>    Symbol.Location=" + l );
                                }
                            }

                        } catch ( Exception e ) {
                            Console.WriteLine( "  =>  Exception: " + e );
                        }
                    }
                    Console.WriteLine( "treeCount=" + treeCount );

                } // foreach-END-OF
            }

            Console.WriteLine("remove the workspace");
            _host.CloseWorkspace( ws );
            Console.WriteLine("COMPLETED!");
        }

        internal RoslynHost GetRoslynHost() {
            return _host;
        }

        internal RoslynWorkspace GetRoslynWorkspace() {
            return ws;
        }

        public List<DocumentInfo> GetAllDocuments() {
            List<DocumentInfo> docs = new List<DocumentInfo>();
            foreach ( ProjectDescriptor_p pd in _projects ) {
                ProjectId projId = pd.GetProjectId();
                foreach( KeyValuePair<DocumentId,DocumentInfo_p> entry in pd.docInfoDict ) {
                    DocumentInfo d = new DocumentInfo();
                    d.ProjId = projId;
                    d.DocId = entry.Key;
                    d.FilePathRel = entry.Value.FilePathRel;
                    docs.Add( d );
                }
            }
            return docs;
        }

        public DocumentId FindDocumentByFilePath( string filePath ) {
            foreach ( ProjectDescriptor_p pd in _projects ) {
                foreach( KeyValuePair<DocumentId,DocumentInfo_p> entry in pd.docInfoDict ) {
                    if ( entry.Value.FilePathRel == filePath ) return entry.Key;
                }
            }
            return null;
        }

// TODO GetCurrentTextFromDocumentObject??
// TODO GetCurrentTextFromDocumentObject??
// TODO GetCurrentTextFromDocumentObject??
        public string GetCurrentText( DocumentId docId ) {
            Document d = sol.GetDocument( docId );
            if ( d != null ) {
                // get the current text directly from Roslyn Document object.
                SourceText st = d.GetTextAsync( CancellationToken.None ).GetAwaiter().GetResult();
                return st.ToString();
            }
            throw new Exception( "ERROR no document found for DocumentId " + docId.ToString() );
        }

// TODO GetCurrentTextFromTextContainer???
// TODO GetCurrentTextFromTextContainer???
// TODO GetCurrentTextFromTextContainer???
        public SourceText GetCurrentTextFromContainer( DocumentId docId ) {
            foreach ( ProjectDescriptor_p pd in _projects ) {
                foreach( KeyValuePair<DocumentId,DocumentInfo_p> entry in pd.docInfoDict ) {
                    if ( entry.Key == docId ) return entry.Value.currentTextContainer.CurrentText;
                }
            }
            throw new Exception( "ERROR no document found for DocumentId " + docId.ToString() );
        }

        public void SyncModifiedContainersToDocuments( DocumentId forSingleDocumentOnly ) {

            // transfer all modified texts from opened editors to Roslyn Projects/Documents.
            // if the parameter is null, all documents are synchronized.

            Console.WriteLine( "SyncAllModifiedContainersToDocuments() : start" );

            foreach ( ProjectDescriptor_p pd in _projects ) {
                ProjectId projId = pd.GetProjectId();
                foreach( KeyValuePair<DocumentId,DocumentInfo_p> entry in pd.docInfoDict ) {

                    DocumentId docId = entry.Key;
                    DocumentInfo_p di = entry.Value;

                    if ( forSingleDocumentOnly != null && forSingleDocumentOnly != docId ) continue;

                    if ( di.TextModificationCount < 1 ) {
                        Console.WriteLine( "SyncAllModifiedContainersToDocuments() : is up-to-date : " + entry.Key );
                        continue;
                    }

                    Document doc = sol.GetProject( projId ).GetDocument( docId );
                    doc = doc.WithText( GetCurrentTextFromContainer( docId ) );
                    di.ResetTextModificationCount();

                    sol = doc.Project.Solution; // solution is updated? so is project.

                    Console.WriteLine( "SyncAllModifiedContainersToDocuments() : updated : " + entry.Key );

                }
            }
        }

        public IDocumentModificationsTracker GetTracker( DocumentId docId ) {
            foreach ( ProjectDescriptor_p pd in _projects ) {
                foreach( KeyValuePair<DocumentId,DocumentInfo_p> entry in pd.docInfoDict ) {
                    if ( entry.Key == docId ) return entry.Value;
                }
            }
            throw new Exception( "ERROR no document found for DocumentId " + docId.ToString() );
        }

        public RoslynCodeEditor GetEditor( DocumentId docId ) {
            foreach ( ProjectDescriptor_p pd in _projects ) {
                foreach( KeyValuePair<DocumentId,DocumentInfo_p> entry in pd.docInfoDict ) {
                    if ( entry.Key == docId ) return entry.Value.currentEditorControl;
                }
            }
            throw new Exception( "ERROR no document found for DocumentId " + docId.ToString() );
        }

        public bool GetIsEditorWindowOpen( DocumentId docId ) {
            Dictionary<DocumentId,string> d = new Dictionary<DocumentId,string>();
            foreach ( ProjectDescriptor_p pd in _projects ) {
                foreach( KeyValuePair<DocumentId,DocumentInfo_p> entry in pd.docInfoDict ) {
                    if ( entry.Key == docId ) return entry.Value.IsEditorWindowOpen;
                }
            }
            throw new Exception( "ERROR no document found for DocumentId " + docId.ToString() );
        }

        public void SetEditorWindowAsOpened( DocumentId docId, RoslynCodeEditor editor, AvalonEditTextContainer container ) {
            Dictionary<DocumentId,string> d = new Dictionary<DocumentId,string>();
            foreach ( ProjectDescriptor_p pd in _projects ) {
                foreach( KeyValuePair<DocumentId,DocumentInfo_p> entry in pd.docInfoDict ) {
                    if ( entry.Key == docId ) {
                        entry.Value.currentEditorControl = editor;
                        entry.Value.currentTextContainer = container;
                        return;
                    }
                }
            }
            throw new Exception( "ERROR no document found for DocumentId " + docId.ToString() );
        }

        public void SetEditorWindowAsClosed( DocumentId docId ) {
            Dictionary<DocumentId,string> d = new Dictionary<DocumentId,string>();
            foreach ( ProjectDescriptor_p pd in _projects ) {
                foreach( KeyValuePair<DocumentId,DocumentInfo_p> entry in pd.docInfoDict ) {
                    if ( entry.Key == docId ) {

                        // take the latest text from textContainer, and store it into Roslyn Document object.
                        // => then we will get the latest text back, if an editor is re-opended for the document.

                        SyncModifiedContainersToDocuments( docId );

                        entry.Value.currentEditorControl = null;
                        entry.Value.currentTextContainer = null;
                        return;
                    }
                }
            }
            throw new Exception( "ERROR no document found for DocumentId " + docId.ToString() );
        }

#region GoToDefinition

        public void GoToDefinition( ProjectId projId, DocumentId docId, int caretPosition ) {

            Console.WriteLine( "GoToDefinition() : docId=" + docId + " caretPosition=" + caretPosition );

            ProjectDescriptor_p pd = null;
            foreach ( ProjectDescriptor_p x in _projects ) {
                if ( x.GetProjectId() == projId ) {
                    pd = x;
                    break;
                }
            }

            if ( pd == null ) {
                Console.WriteLine( "ERROR: no such project found: " + projId );
                return;
            }

            if ( pd.docInfoDict.TryGetValue( docId, out DocumentInfo_p docInfo ) == false ) {
                Console.WriteLine( "ERROR: no such document found: " + docId );
                return;
            }

            string filePathRel = docInfo.FilePathRel;
            Console.WriteLine( "GoToDefinition() : filePathRel=" + filePathRel );

            // now update all texts modified in opened editors to Roslyn Document objects.
            SyncModifiedContainersToDocuments( null );

            // write the current text from Roslyn Document object to console (just a debug step).
            Document doc = sol.GetProject( projId ).GetDocument( docId );
            string txt = GetCurrentText( docId );
            Console.WriteLine( txt );

            // get a Compilation object for the project, and find a SyntaxTree which
            // which corresponds to the given DocumentId.

            // TODO does it matter to use Compilation vs CSharpCompilation?
            //CSharpCompilation comp = (CSharpCompilation) sol.GetProject( projId ).GetCompilationAsync().GetAwaiter().GetResult();
            CSharpCompilation comp = (CSharpCompilation) doc.Project.GetCompilationAsync().GetAwaiter().GetResult();

            SyntaxTree st = null;
            foreach ( var tree in comp.SyntaxTrees ) {
                if ( tree.FilePath == filePathRel ) {
                    st = tree;
                    break;
                }
            }

            if ( st == null ) {
                Console.WriteLine( "ERROR: no SyntaxTree found for FilePath: " + filePathRel );
                return;
            }

            // from SyntaxTree find a SyntaxToken which matches to given caretPosition.
            // based on SyntaxToken get semantic symbol-information.

            try {
                CompilationUnitSyntax root = st.GetCompilationUnitRoot();
                SyntaxToken tok = root.FindToken( caretPosition, false );

                Console.WriteLine( "token in position " + caretPosition + " is " + tok.GetType() + " : '" + tok.Text + "'" );

                SyntaxNode sn = tok.Parent;
                if ( sn == null ) {
                    Console.WriteLine( "  =>  SyntaxNode missing." );
                } else {
                    SemanticModel model = comp.GetSemanticModel( st );
                    SymbolInfo si = model.GetSymbolInfo( sn );

// https://docs.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.isymbol?view=roslyn-dotnet-4.2.0
                    ISymbol s = si.Symbol;
                    if ( s != null ) {
                        Console.WriteLine( "  =>  got Symbol with Locations count=" + s.Locations.Length );

                        if ( s.Locations.Length > 0 ) {
                            Location loc = s.Locations[0]; // take the first one...
                            if ( loc.IsInMetadata ) {
                                Console.WriteLine( "  =>  TODO Symbol location is in METADATA." );
                            } else if ( loc.IsInSource ) {

                                if ( loc.SourceTree != null ) {

// TODO all we get here is FilePath (there is NO ProjectId etc extra info supplied).
// => the FilePath information should be able to UNIQUELY identify the files in all of the projects.
// => so FilePath should NOT be just a local path inside the project...

                                    string filePath = loc.SourceTree.FilePath;
                                    TextSpan span = loc.SourceSpan;

                                    Console.WriteLine( "  =>  Symbol location is in source: " + filePath + " start=" + span.Start + " length=" + span.Length );

                                    DocumentId d = CsEditWorkspace.Instance.FindDocumentByFilePath( filePath );
                                    RoslynCodeEditor editor = CsEditWorkspace.Instance.GetEditor( d );
                                    if ( editor == null ) {
                                        Console.WriteLine( "  =>  document is closed." );
// TODO open the document, and add an extra param about selection???
// TODO open the document, and add an extra param about selection???
// TODO open the document, and add an extra param about selection???
                                    } else {
                                        editor.Select( span.Start, span.Length );
                                    }
                                }

                            } else {
                                Console.WriteLine( "  =>  TODO Symbol location information missing." );
                            }
                        }
                    }
                }
            } catch ( Exception e ) {
                Console.WriteLine( "Exception: " + e.ToString() );
            }
        }

#endregion // GoToDefinition

        public static void DumpSolutionContents( Solution sol ) {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine( "dump SOLUTION contents:" );
            foreach ( var p in sol.Projects ) {
                Console.WriteLine( "    PROJECT " + p.Name );
                foreach ( var a in p.MetadataReferences ) {
                    Console.WriteLine( "        Assembly " + a.Display );
                }
                foreach ( var x in p.ProjectReferences ) {
                    Console.WriteLine( "        ProjectReference " + x.ToString() );
                }
                foreach ( var d in p.Documents ) {
                    Console.WriteLine( "        DOCUMENT " + d.Name );
                }
            }
            Console.WriteLine();
            Console.WriteLine();
        }

        // inner classes start...
        // inner classes start...
        // inner classes start...

#region ProjectDescriptor_p

    public class ProjectDescriptor_p
    {
        // TODO need to have private setters here + related sanity-checks.
        public List<string> SourceFiles { get; set; }
        public List<string> LibraryFiles { get; set; }

        // TODO how to check validity of these???
        public List<string> ProjectReferences { get; set; }

        public string Name { get; private set; }

        private string libraryPath;
        internal Dictionary<DocumentId,DocumentInfo_p> docInfoDict;

        private ProjectId pId;

        public ProjectDescriptor_p( string name, string libPath )
        {
            Name = name;
            libraryPath = libPath;
            docInfoDict = new Dictionary<DocumentId,DocumentInfo_p>();

            SourceFiles = new List<string>();
            LibraryFiles = new List<string>();

            ProjectReferences = new List<string>();

            pId = null;
        }

        public ProjectId CreateProject( RoslynHost host, RoslynWorkspace ws, ref Solution sol, CompilationOptions compilationOptions, List<ProjectReference> projectReferences )
        {
            // create a new project (previous must not exist).
            if ( pId != null ) throw new Exception("project already exists!");

            Project project = host.CreateProject_alt( ws, ref sol, Name, SourceCodeKind.Regular, compilationOptions, projectReferences );
            pId = project.Id;

            // add all specified libraries and sourcefiles.
            // => it seems that the libraries must be added first.

            foreach ( string libFile in LibraryFiles ) {
                string libPath = libraryPath + "/" + libFile;
                MetadataReference mdr = MetadataReference.CreateFromFile( libPath );
                host.AddMetadataReference_alt( ws, ref sol, ref project, mdr );
            }

            foreach ( string srcFilePath in SourceFiles ) {

                // NOTE srcFilePath can be either a plain filename, or a relative path.
                // BUT it must remain unique at workspace-level.

                if ( CsEditWorkspace.Instance.FindDocumentByFilePath( srcFilePath ) != null ) {
                    throw new Exception( "ERROR filePath already exists: " + srcFilePath );
                }

                string initialText = File.ReadAllText( srcFilePath );
                DocumentId d_Id = host.AddDocument_alt( ws, ref sol, ref project, null, srcFilePath, initialText );

                DocumentInfo_p docInfo = new DocumentInfo_p( d_Id, srcFilePath );
                docInfoDict.Add( d_Id, docInfo );

                // open the document now, in order to get feedback...

                AvaloniaEdit.Document.TextDocument d = new AvaloniaEdit.Document.TextDocument( initialText );
                AvalonEditTextContainer c = new AvalonEditTextContainer( d );
                host.OpenDocument_alt( ws, d_Id, c, PrintFeedback, null );
            }

            return pId;
        }

        public ProjectId GetProjectId()
        {
            if ( pId == null ) throw new Exception( "GetProjectId() : pId is null (project not yet created)." );
            return pId;
        }

        public void AddProjectReference( RoslynHost host, RoslynWorkspace ws, ref Solution sol, ProjectReference pr )
        {
            Project project = sol.GetProject( pId );
            project = project.AddProjectReference( pr );
            sol = project.Solution;
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
                if ( docInfoDict.TryGetValue( d.DocumentId, out DocumentInfo_p docInfo ) ) {
                    fileName = docInfo.FilePathRel;
                } else {
                    fileName = "<unknown>"; // no filename registered for the DocumentId.
                }

                Console.WriteLine( d.Severity.ToString() + " at line " + line + " position " + position + " : " + fileName );
                Console.WriteLine( "  T->    " + d.Title ); // sometimes the same as previous?
                Console.WriteLine( "  M->    " + d.Message );
            }
            Console.WriteLine();
	}
    }

#endregion // ProjectDescriptor_p

#region DocumentInfo_p

    public class DocumentInfo_p : IDocumentModificationsTracker {

        public DocumentId DocId { get; private set; }
        public string FilePathRel { get; private set; } // FilePathUniq

        public RoslynCodeEditor currentEditorControl { get; set; }
        public AvalonEditTextContainer currentTextContainer { get; set; }

        public bool IsEditorWindowOpen { get { return currentEditorControl != null; } }

// TODO need to have 2 separate modification counters:
// 1) modifications since last sync to Roslyn Document object (the current counter).
// 2) modifications since last save to file (TODO...)
        public int TextModificationCount { get; private set; }

        public DocumentInfo_p( DocumentId docId, string filePath ) {
            DocId = docId;
            FilePathRel = filePath;

            currentEditorControl = null;
            currentTextContainer = null;

            ResetTextModificationCount();
        }

        public void DocumentModified() {
            TextModificationCount++;
        }

        public void ResetTextModificationCount() {
            TextModificationCount = 0;
        }
    }

#endregion // DocumentInfo_p

    }
}
