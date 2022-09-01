
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

//using AvaloniaEdit.Document; // TextDocument

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using System.Linq; // oftype

using Microsoft.CodeAnalysis; // DocumentId etc
using Microsoft.CodeAnalysis.Text; // TextSpan etc

namespace CsEdit.Avalonia
{


    public class MainWindowViewModel : IEditorWindowEventListener
    {
        // we use this class as a singleton in practice,
        // but do not have private constructor etc formal stuff here.
        public static MainWindowViewModel Instance { get; private set; } = null;

        private List<DocumentDescriptor> allDocs = null;

        public ObservableCollection<TreeItemProject> Projects { get; }

        public MainWindowViewModel()
        {
            if ( Instance != null ) { 
                throw new InvalidOperationException( "MainWindowViewModel singleton-instance already exists!" );
            }

            Instance = this;

            // this is where CsEditWorkspace will be initialized...
            CsEditWorkspace.Instance.Listener = this;
            allDocs = CsEditWorkspace.Instance.GetAllDocuments();
            Console.WriteLine( "allDocs.Count = " + allDocs.Count );

            Projects = new ObservableCollection<TreeItemProject>( InitProjects() );
            Console.WriteLine( "MainWindowViewModel : Projects.Count = " + Projects.Count );
        }

        private List<TreeItemProject> InitProjects()
        {
            List<ProjectDescriptor> pList = ProjectsProvider.Projects;

            List<TreeItemProject> result = new List<TreeItemProject>();

            foreach ( ProjectDescriptor pd in pList ) {

                TreeItemProject p = new TreeItemProject();
                p.ProjectName = pd.ProjectNameUniq;

                foreach ( string fileName in pd.SourceFiles ) {

                    TreeItemFile f = new TreeItemFile();
                    f.FileName = fileName;
                    p.Files.Add( f );

                    // link TreeItemFile to each document.
                    foreach( DocumentDescriptor dd in allDocs ) {

                        if ( dd.ProjectNameUniq != pd.ProjectNameUniq ) continue;
                        if ( dd.FilePathUniq != fileName ) continue;

                        dd.TreeItem = f;
                        break;
                    }
                }

                result.Add( p );
            }

            return result;
        }



        //public void HandleButtonClick( MainWindow ctrl, string cmdParam ) {
        public void CreateOrShowEditorWindow( string filePathUniq, TextSpan? span ) {

// the first parameter "ctrl" should always be the MainWindow (singleton-) instance.
MainWindow ctrl = MainWindow.Instance;

            // the first parameter is the unique document filepath, for which the operation is targeted.
            // the second (optional) parameter is a text-span to be selected/highligted in the document.

            bool buttonFound = false;

            foreach( DocumentDescriptor dd in allDocs ) {

                if ( dd.FilePathUniq != filePathUniq ) continue;

                buttonFound = true;

                if ( CsEditWorkspace.Instance.GetIsEditorWindowOpen( dd.DocumentId ) ) {

                    //Console.WriteLine( "activate window : " + dd.DocumentId.ToString() );

                    CsEditCodeEditorWindow editor = CsEditWorkspace.Instance.GetEditorWindow( dd.DocumentId );
if ( span.HasValue ) editor.SelectAndShowTextSpan( span.Value.Start, span.Value.Length );
                    editor.ActivateWindow();

                    // the button event is now processed.
                    // => no return here however, since we want to log problems (see "buttonFound").
                    continue;
                }

                // open a new editor window.

                var wnd = new CsEditCodeEditorWindow();

                // set the EditorWindow property ASAP, so that it
                // will be available in IEditorWindowEventListener methods!
                dd.EditorWindow = wnd;

                wnd.Init( dd.ProjectId, dd.DocumentId, dd.FilePathUniq );
                wnd.Show();


// VALINTA EXTRA...
// VALINTA EXTRA...
// VALINTA EXTRA... 
/*
RoslynCodeEditor editor = CsEditWorkspace.Instance.GetEditor( d );
                                        // first make sure that the new selection will be visible.
                                        // NOTICE the methods like TextEditor.ScollTo(line,column) are not working?
                                        editor.TextArea.Caret.Offset = 10; //span.Start;
                                        editor.TextArea.Caret.BringCaretToView();
                                        // then make th actual selection.
                                        editor.Select( 10, 10 ); //span.Start, span.Length );*/
//wnd.SelectAndShowTextSpan( 10, 10 );
if ( span.HasValue ) wnd.SelectAndShowTextSpan( span.Value.Start, span.Value.Length );





                Console.WriteLine( "Opened a new editor window : " + dd.DocumentId.ToString() );
                return;
            }

            if ( buttonFound ) return;
            Console.WriteLine( "ERROR: button control not found!" );
        }



// https://github.com/AvaloniaUI/Avalonia/issues/3085 
// http://reference.avaloniaui.net/api/Avalonia.VisualTree/VisualExtensions/ 
// http://reference.avaloniaui.net/api/Avalonia.Controls/TreeViewItem/ 
        private static T FindSubControl<T>( Control parent, string ctrlName ) where T : Control {
            foreach ( T ctrl in parent.GetVisualDescendants().OfType<T>() ) {
                if ( ctrl.Name != ctrlName ) continue;
                return ctrl;
            }
            return default( T );
        }



        public void EditorWindowOpened( DocumentId docId ) {

            //Console.WriteLine( "got an EditorWindowOpened event for " + docId );

            foreach( DocumentDescriptor dd in allDocs ) {
                if ( dd.DocumentId != docId ) continue;

                // change the button text to indicate that the document has been opened.
                Button b = FindSubControl<Button>( MainWindow.Instance, dd.FilePathUniq );
                if ( b != null ) b.Content = "show";

                return;
            }
        }

        public void EditorWindowClosed( DocumentId docId ) {

            //Console.WriteLine( "got an EditorWindowClosed event for " + docId );

            foreach( DocumentDescriptor dd in allDocs ) {
                if ( dd.DocumentId != docId ) continue;

                // change the button text to indicate that the document has been closed.
                Button b = FindSubControl<Button>( MainWindow.Instance, dd.FilePathUniq );
                if ( b != null ) b.Content = "open";

                // update the DocumentDescriptor object.
                dd.EditorWindow = null;

                return;
            }
        }

        public void DocumentIsModified( DocumentId docId, bool isModified ) {

Console.WriteLine( "MOD: " + docId + " isModified=" + isModified );

        }
    }

}
