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

namespace CsEdit.Avalonia {

    public class ProjectDescriptor {

        public string ProjectNameUniq { get; private set; }
        public string TargetFramework { get; private set; }

        public string[] SourceFiles { get; private set; }

        public string[] LibraryFiles { get; private set; }

        public string[] ProjectReferences { get; private set; }
        public Dictionary<string,string> PackageReferences { get; private set; }

        public LanguageVersion LanguageVersion { get; private set; }
        public bool NullableReferenceTypes { get; private set; }

        public ProjectDescriptor( string name, string target, string[] srcFiles, string[] projRefs, Dictionary<string,string> pkgRefs, LanguageVersion lv, bool nrt ) {

            ProjectNameUniq = name;
            TargetFramework = target;

            // libraries are set in a later step...
            LibraryFiles = null;

            SourceFiles = srcFiles;
            ProjectReferences = projRefs;
            PackageReferences = pkgRefs;

            LanguageVersion = lv;
            NullableReferenceTypes = nrt;
        }

        public void SetLibraries( string[] libFiles ) {
            if ( LibraryFiles != null ) throw new Exception( "libraries are set already!" );
            LibraryFiles = libFiles;
        }
    }

    // the idea of "DocumentDescriptor" is to transfer the projects data to MainWindowViewModel,
    // while keeping the master data protected under "CsEditWorkspace".

    public class DocumentDescriptor {

        public ProjectId ProjectId;
        public string ProjectNameUniq;

        public DocumentId DocumentId;
        public string FilePathUniq;

        public TreeItemFile TreeItem;
        public CsEditCodeEditorWindow EditorWindow;
    }

    // https://docs.microsoft.com/en-us/aspnet/web-forms/overview/deployment/web-deployment-in-the-enterprise/understanding-the-project-file 
    // https://natemcmaster.com/blog/2017/03/09/vs2015-to-vs2017-upgrade/ 

    public class RuntimeConfig {
        public string Runtime;
        public string TargetFramework;
        public LanguageVersion LanguageVersion; // the HIGHEST value from projects
        //public bool NullableReferenceTypes; 20220809 moved into ProjectDescriptor
    }

    public interface IProjectReader {
        bool TryRead( string dirPathRel, List<ProjectDescriptor> pList, out RuntimeConfig cfg );
    }

    public static class ProjectsProvider {

        public static string WorkingDirectory { get; private set; }
        public static RuntimeConfig RuntimeConfig { get; private set; }
        public static List<ProjectDescriptor> Projects { get; private set; }

        static ProjectsProvider() {
            WorkingDirectory = null;
            RuntimeConfig = null;
            Projects = null;
        }

        public static void Init( string wrkdir ) {

            // this method either succeeds, or throws an exception.

            if ( string.IsNullOrWhiteSpace( wrkdir ) ) {
                throw new Exception( "Parameter wrkdir is null or empty." );
            }

            Console.WriteLine( "CurrentDirectory = " + Environment.CurrentDirectory );

            wrkdir = Path.GetFullPath( wrkdir );

            Console.WriteLine( "WorkingDirectory = " + wrkdir );

            if ( Directory.Exists( wrkdir ) == false ) {
                throw new Exception( "WorkingDirectory is not valid!" );
            }

            // WorkingDirectory
            // => is the current directory, if not otherwise specified using a commandline argument.
            // => is always an absolute path (the cmdline argument can be either a relative or an absolute path).
            // => is the directory from where the primary project file is searched from.
            // => if the primary project has references to there projects, these are read in as well (recursively).
            // => for the other projects a project-path relative the the primary project will be assigned.

            WorkingDirectory = wrkdir;
            Projects = new List<ProjectDescriptor>();

            // ok, it seems that the given WorkingDirectory is a real directory.
            // now try to find and parse a valid project file (at least one is required).

            IProjectReader[] readers = new IProjectReader[] {
                new NewVsProjectFileReader()
            };

            foreach ( IProjectReader reader in readers ) {

                if ( reader.TryRead( ".", Projects, out RuntimeConfig cfg ) ) {
                    // if any of the readers report success, then leave the reader loop.
                    RuntimeConfig = cfg;
                    break;
                }

                // if a reader fails, it should leave the Projects-list intact.
                if ( Projects.Count != 0 ) throw new Exception( "Reader failed but Projects not empty." );
            }

            if ( Projects.Count < 1 ) {
                throw new Exception( "No projects found!" );
            }

            int totalSourceFiles = 0;
            foreach ( ProjectDescriptor pd in Projects ) {
                if ( pd.SourceFiles != null ) totalSourceFiles += pd.SourceFiles.Length;
            }

            if ( totalSourceFiles < 1 ) {
                throw new Exception( "No source files found!" );
            }
        }

        public static string FindFirstFileWithExtension( string dirPathRel, string extension ) {

            // extensions are given here without the dot-separator, e.g. as "csproj".

            // filename of a .csproj file may vary, but only one .csproj file is allowed in a single directory.
            // also .sln and .csproj file are not allowed to coexist in a same directory.

            string searchPath = WorkingDirectory + Path.DirectorySeparatorChar + dirPathRel;
            string[] items = Directory.GetFiles( searchPath, "*." + extension );

            if ( items.Length > 0 ) {
                string resultPath = items[0];
                if ( File.Exists( resultPath ) == false ) return null;
                return Path.GetFileName( resultPath );
            }

            return null;
        }
    }



    public interface IEditorWindowEventListener {
        void EditorWindowOpened( DocumentId docId );
        void EditorWindowClosed( DocumentId docId );
// TODO need to add the modified/saved events here as well???
// TODO need to add the modified/saved events here as well???
// TODO need to add the modified/saved events here as well???
    }



    public class CsEditWorkspace {

        private List<ProjectInfo_p> _projects;
        // TODO should this be a Dictionary<ProjectId,ProjectInfo_p>???
        // NO because we will populate this later (when ProjectId values are known).

        private RoslynHost _host;

        private RoslynWorkspace _ws;
        private Solution _sol;

        // currently we need to have just one listener (MainWindowViewModel).
        public IEditorWindowEventListener Listener { get; set; }

        #region singleton

        private static CsEditWorkspace _instance = null;
        private static readonly object _instanceLock = new object();

        public static CsEditWorkspace Instance {
            get {
                lock ( _instanceLock ) {
                    if ( _instance == null ) {
                        _instance = new CsEditWorkspace();
                        _instance.Init();
                    }
                    return _instance;
                }
            }
        }

        private CsEditWorkspace() {
            // NOTICE the method Init() will access the Instance property,
            // so calling it directly from here would cause an infinite loop.
        }

        #endregion // singleton

        private void Init() {
            Console.WriteLine( "Init() starting" );

            string wrkdir = ProjectsProvider.WorkingDirectory;
            if ( string.IsNullOrWhiteSpace( wrkdir ) ) throw new Exception( "WorkingDirectory is not valid!" );

            // ProjectsProvider.Projects should exist now, and contain project(s) with at least one src-file.

            string runtime = ProjectsProvider.RuntimeConfig.Runtime;
            string targetFramework = ProjectsProvider.RuntimeConfig.TargetFramework;

            Console.WriteLine( "Init() runtime=" + runtime );
            Console.WriteLine( "Init() targetFramework=" + targetFramework );
            Console.WriteLine( "Init() langVersion=" + ProjectsProvider.RuntimeConfig.LanguageVersion );

            _host = new RoslynHost( ProjectsProvider.RuntimeConfig.LanguageVersion,
            additionalAssemblies: new[]
            {
                //Assembly.Load("RoslynPad.Roslyn.Windows"),
                //Assembly.Load("RoslynPad.Editor.Windows")

                // read these extra MEF components etc...
                Assembly.Load("RoslynPad.Roslyn.Avalonia"),
                Assembly.Load("RoslynPad.Editor.Avalonia")

            }, RoslynHostReferences.NamespaceDefault
            /* skip... also see src/RoslynPad.Roslyn/RoslynHostReferences.cs about namespaces?
            .With(assemblyReferences: new[]
            {

                // TODO are all these relevant?
                // TODO need to add something more here?

                // do not add any fixed assemblies here.
                // there is the mono/dotnetSKD issue (and maybe more version issues)?

                //typeof(object).Assembly,
                //typeof(System.Text.RegularExpressions.Regex).Assembly,
                //typeof(System.Linq.Enumerable).Assembly,
            }) */
            );

            Console.WriteLine( "RoslynHost created" );
            Console.WriteLine( "creating workspace/solution" );

            _ws = _host.CreateWorkspace();
            _sol = _ws.CurrentSolution;

            _host.AddAnalyzerReferences( _ws, ref _sol );

            _projects = new List<ProjectInfo_p>();

            foreach ( ProjectDescriptor pd in ProjectsProvider.Projects ) {

                Console.WriteLine();
                Console.WriteLine( "CREATING PROJECT: " + pd.ProjectNameUniq + " for " + pd.TargetFramework + " NRT=" + pd.NullableReferenceTypes );
                Console.WriteLine();

                _projects.Add( new ProjectInfo_p( pd ) );
            }

            // IMPORTANT! it seems thet the project tree must be built from ground-up.
            // giving all project references as parameters to project-creation step.
            // => trying to add the references afterwards does not work.

            // TODO the projects are not yet sorted based on dependencies? need to check this.
            // TODO how to detect cyclic dependencies (correctly?). this is ok (each project created only once).

            foreach ( ProjectInfo_p x in _projects ) {

                Console.WriteLine();
                Console.Write("the project \"" + x.desc.ProjectNameUniq + "\" contains ");
                Console.Write(x.desc.SourceFiles.Length + " source files and ");
                Console.Write(x.desc.LibraryFiles.Length + " library files.");
                Console.WriteLine();

                List<ProjectReference> projectReferences = new List<ProjectReference>();

                foreach ( string projName in x.desc.ProjectReferences ) {

                    ProjectId pId = null;
                    foreach ( ProjectInfo_p y in _projects ) {
                        if ( y.desc.ProjectNameUniq != projName ) continue;
                        pId = y.GetProjectId();
                    }

                    if ( pId == null ) {
                        Console.WriteLine( "ERROR project not found: " + projName );
                        return;
                    }

                    Console.WriteLine( "  =>  adding project reference: " + x.desc.ProjectNameUniq + " => " + projName );

                    ProjectReference pref = new ProjectReference( pId );
                    projectReferences.Add( pref );
                }

                Console.WriteLine();
                Console.WriteLine( "creating the project: " + x.desc.ProjectNameUniq );

                CompilationOptions compilationOptions = _host.CreateCompilationOptions_alt( x.desc.NullableReferenceTypes );

                ProjectId projectId = x.CreateProject( _host, _ws, ref _sol, compilationOptions, projectReferences );
            }

            // since the Solution and Project objects are immutable, various operations will
            // generate updated objects, and therefore existing objects can become obsoleted.
            // => always keep an up-to-date version of the Solution instance.
            // => avoid keeping Project objects, prefer getting fresh Project objects from Solution (using ProjectId).

            DumpSolutionContents( _sol );

            Console.WriteLine();
            Console.WriteLine( "solution setup is now READY." );
            Console.WriteLine();

            //Console.WriteLine( "remove the workspace" );
            //_host.CloseWorkspace( ws );
            //Console.WriteLine( "COMPLETED!" );
        }

        internal RoslynHost GetRoslynHost() {
            return _host;
        }

        internal RoslynWorkspace GetRoslynWorkspace() {
            return _ws;
        }

        public List<DocumentDescriptor> GetAllDocuments() {
            List<DocumentDescriptor> docs = new List<DocumentDescriptor>();
            foreach ( ProjectInfo_p pd in _projects ) {
                ProjectId projId = pd.GetProjectId();
                foreach( KeyValuePair<DocumentId,DocumentInfo_p> entry in pd.docInfoDict ) {
                    DocumentDescriptor dd = new DocumentDescriptor();

                    dd.ProjectId = projId;
                    dd.ProjectNameUniq = pd.desc.ProjectNameUniq;

                    dd.DocumentId = entry.Key;
                    dd.FilePathUniq = entry.Value.FilePathUniq;

                    // MainWindowViewModel will set this.
                    dd.TreeItem = null;

                    docs.Add( dd );
                }
            }
            return docs;
        }

        public DocumentId FindDocumentByFilePath( string filePath ) {
            foreach ( ProjectInfo_p pd in _projects ) {
                foreach( KeyValuePair<DocumentId,DocumentInfo_p> entry in pd.docInfoDict ) {
                    if ( entry.Value.FilePathUniq == filePath ) return entry.Key;
                }
            }
            return null;
        }

// TODO GetCurrentTextFromDocumentObject??
// TODO GetCurrentTextFromDocumentObject??
// TODO GetCurrentTextFromDocumentObject??
        public string GetCurrentText( DocumentId docId ) {
            Document d = _sol.GetDocument( docId );
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
            foreach ( ProjectInfo_p pd in _projects ) {
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

            foreach ( ProjectInfo_p pd in _projects ) {
                ProjectId projId = pd.GetProjectId();
                foreach( KeyValuePair<DocumentId,DocumentInfo_p> entry in pd.docInfoDict ) {

                    DocumentId docId = entry.Key;
                    DocumentInfo_p di = entry.Value;

                    if ( forSingleDocumentOnly != null && forSingleDocumentOnly != docId ) continue;

                    if ( di.TextModificationCount < 1 ) {
                        Console.WriteLine( "SyncAllModifiedContainersToDocuments() : is up-to-date : " + entry.Key );
                        continue;
                    }

                    Document doc = _sol.GetProject( projId ).GetDocument( docId );
                    doc = doc.WithText( GetCurrentTextFromContainer( docId ) );
                    di.ResetTextModificationCount();

                    // solution is updated?
                    // the same is for project, but we don't use it here.
                    _sol = doc.Project.Solution;

                    Console.WriteLine( "SyncAllModifiedContainersToDocuments() : updated : " + entry.Key );

                }
            }
        }

        public IDocumentModificationsTracker GetTracker( DocumentId docId ) {
            foreach ( ProjectInfo_p pd in _projects ) {
                foreach( KeyValuePair<DocumentId,DocumentInfo_p> entry in pd.docInfoDict ) {
                    if ( entry.Key == docId ) return entry.Value;
                }
            }
            throw new Exception( "ERROR no document found for DocumentId " + docId.ToString() );
        }

        public CsEditCodeEditorWindow GetEditorWindow( DocumentId docId ) {
            foreach ( ProjectInfo_p pd in _projects ) {
                foreach( KeyValuePair<DocumentId,DocumentInfo_p> entry in pd.docInfoDict ) {
                    if ( entry.Key == docId ) return entry.Value.currentEditorWindow;
                }
            }
            throw new Exception( "ERROR no document found for DocumentId " + docId.ToString() );
        }

        public bool GetIsEditorWindowOpen( DocumentId docId ) {
            Dictionary<DocumentId,string> d = new Dictionary<DocumentId,string>();
            foreach ( ProjectInfo_p pd in _projects ) {
                foreach( KeyValuePair<DocumentId,DocumentInfo_p> entry in pd.docInfoDict ) {
                    if ( entry.Key == docId ) return entry.Value.IsEditorWindowOpen;
                }
            }
            throw new Exception( "ERROR no document found for DocumentId " + docId.ToString() );
        }

        public void SetEditorWindowAsOpened( DocumentId docId, CsEditCodeEditorWindow editorWindow, AvalonEditTextContainer textContainer ) {
            Dictionary<DocumentId,string> d = new Dictionary<DocumentId,string>();
            foreach ( ProjectInfo_p pd in _projects ) {
                foreach( KeyValuePair<DocumentId,DocumentInfo_p> entry in pd.docInfoDict ) {
                    if ( entry.Key == docId ) {
                        entry.Value.currentEditorWindow = editorWindow;
                        entry.Value.currentTextContainer = textContainer;

                        if ( Listener != null ) Listener.EditorWindowOpened( docId );

                        return;
                    }
                }
            }
            throw new Exception( "ERROR no document found for DocumentId " + docId.ToString() );
        }

        public void SetEditorWindowAsClosed( DocumentId docId ) {
            Dictionary<DocumentId,string> d = new Dictionary<DocumentId,string>();
            foreach ( ProjectInfo_p pd in _projects ) {
                foreach( KeyValuePair<DocumentId,DocumentInfo_p> entry in pd.docInfoDict ) {
                    if ( entry.Key == docId ) {

                        // take the latest text from textContainer, and store it into Roslyn Document object.
                        // => then we will get the latest text back, if an editor is re-opended for the document.

                        SyncModifiedContainersToDocuments( docId );

                        entry.Value.currentEditorWindow = null;
                        entry.Value.currentTextContainer = null;

                        if ( Listener != null ) Listener.EditorWindowClosed( docId );

                        return;
                    }
                }
            }
            throw new Exception( "ERROR no document found for DocumentId " + docId.ToString() );
        }

#region GoToDefinition

        public void GoToDefinition( ProjectId projId, DocumentId docId, int caretPosition ) {

            Console.WriteLine( "GoToDefinition() : docId=" + docId + " caretPosition=" + caretPosition );

            ProjectInfo_p pd = null;
            foreach ( ProjectInfo_p x in _projects ) {
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

            string filePathUniq = docInfo.FilePathUniq;
            Console.WriteLine( "GoToDefinition() : filePathUniq=" + filePathUniq );

            // now update all texts modified in opened editors to Roslyn Document objects.
            SyncModifiedContainersToDocuments( null );

            // write the current text from Roslyn Document object to console (just a debug step).
            Document doc = _sol.GetProject( projId ).GetDocument( docId );
            string txt = GetCurrentText( docId );
            Console.WriteLine( txt );

            // get a Compilation object for the project, and find a SyntaxTree which
            // which corresponds to the given DocumentId.

            // TODO does it matter to use Compilation vs CSharpCompilation?
            //CSharpCompilation comp = (CSharpCompilation) sol.GetProject( projId ).GetCompilationAsync().GetAwaiter().GetResult();
            CSharpCompilation comp = (CSharpCompilation) doc.Project.GetCompilationAsync().GetAwaiter().GetResult();

            SyntaxTree st = null;
            foreach ( var tree in comp.SyntaxTrees ) {
                if ( tree.FilePath == filePathUniq ) {
                    st = tree;
                    break;
                }
            }

            if ( st == null ) {
                Console.WriteLine( "ERROR: no SyntaxTree found for FilePath: " + filePathUniq );
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
                                    CsEditCodeEditorWindow editor = CsEditWorkspace.Instance.GetEditorWindow( d );
                                    if ( editor == null ) {
                                        // open a new editor-window...
                                        MainWindowViewModel.Instance.CreateOrShowEditorWindow( filePath, span );
                                    } else {
                                        // activate an existing editor-window...
                                        editor.SelectAndShowTextSpan( span.Start, span.Length );
                                    }
                                }

                            } else {
                                Console.WriteLine( "  =>  TODO Symbol location information missing." );
                            }
                        }
                    } else {
                        Console.WriteLine( "  =>  Symbol not found." );
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
                    string name1 = p.Name;
                    string name2 = sol.GetProject( x.ProjectId ).Name;
                    Console.WriteLine( "        ProjectReference " + name1 + " => " + name2 );
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

#region ProjectInfo_p

    public class ProjectInfo_p
    {
        internal ProjectDescriptor desc;

        internal Dictionary<DocumentId,DocumentInfo_p> docInfoDict;

        private ProjectId pId;

        public ProjectInfo_p( ProjectDescriptor pd )
        {
            desc = pd;

            docInfoDict = new Dictionary<DocumentId,DocumentInfo_p>();

            pId = null;
        }

        public ProjectId CreateProject( RoslynHost host, RoslynWorkspace ws, ref Solution sol, CompilationOptions compilationOptions, List<ProjectReference> projectReferences )
        {
            // create a new project (previous must not exist).
            if ( pId != null ) throw new Exception("project already exists!");

            Project project = host.CreateProject_alt( ws, ref sol, desc.ProjectNameUniq, SourceCodeKind.Regular, compilationOptions, projectReferences );
            pId = project.Id;

            // add all specified libraries and sourcefiles.
            // => it seems that the libraries must be added first.

            foreach ( string libFile in desc.LibraryFiles ) {

                // libFile may be either a filename, or an absolute path.
// TODO...
// TODO... need to extend this?
// TODO...

                string libPath;
                if ( Path.IsPathRooted( libFile ) ) libPath = libFile;
                else throw new Exception( "lib filepath must be absolute: " + libFile );

                MetadataReference mdr = MetadataReference.CreateFromFile( libPath );
                host.AddMetadataReference_alt( ws, ref sol, ref project, mdr );
            }

            foreach ( string srcFilePath in desc.SourceFiles ) {

                // NOTE srcFilePath can be either a plain filename, or a relative path.
                // BUT it must remain unique at workspace-level.

                if ( CsEditWorkspace.Instance.FindDocumentByFilePath( srcFilePath ) != null ) {
                    throw new Exception( "ERROR filePath already exists: " + srcFilePath );
                }

                string initialText = File.ReadAllText( ProjectsProvider.WorkingDirectory + Path.DirectorySeparatorChar + srcFilePath );
                DocumentId d_Id = host.AddDocument_alt( ws, ref sol, ref project, null, srcFilePath, initialText );

                DocumentInfo_p docInfo = new DocumentInfo_p( d_Id, srcFilePath );
                docInfoDict.Add( d_Id, docInfo );
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
                    fileName = docInfo.FilePathUniq;
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

#endregion // ProjectInfo_p

#region DocumentInfo_p

    public class DocumentInfo_p : IDocumentModificationsTracker {

        public DocumentId DocId { get; private set; }
        public string FilePathUniq { get; private set; }

        public CsEditCodeEditorWindow currentEditorWindow { get; set; }
        public AvalonEditTextContainer currentTextContainer { get; set; }

        public bool IsEditorWindowOpen { get { return currentEditorWindow != null; } }

// TODO eventually need to have 2 separate modification counters:
// 1) modifications since last sync to Roslyn Document object (the current counter).
// 2) modifications since last saving to file (TODO...)
        public int TextModificationCount { get; private set; }

        public DocumentInfo_p( DocumentId docId, string filePath ) {
            DocId = docId;
            FilePathUniq = filePath;

            currentEditorWindow = null;
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
