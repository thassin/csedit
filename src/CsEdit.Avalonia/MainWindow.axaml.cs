using Avalonia.Controls;
using Avalonia.Interactivity; // RoutedEventArgs

using Microsoft.CodeAnalysis; // SourceCodeKind etc
using Microsoft.CodeAnalysis.CSharp; // LanguageVersion etc

using System;
using System.Collections.Generic;
using System.ComponentModel; // CancelEventArgs
using System.Linq; // for Dictionary.ElementAt()...

namespace CsEdit.Avalonia
{
    public partial class MainWindow : Window
    {
        private List<DocumentInfo> allDocs = null;

        public MainWindow()
        {
            InitializeComponent();
            Closing += OnClosing;

            // this is where CsEditWorkspace will be initialized...
            allDocs = CsEditWorkspace.Instance.GetAllDocuments();
            Console.WriteLine( "allDocs.Count = " + allDocs.Count );

            /* open all windows...
            foreach( DocumentInfo d in allDocs ) {
                var wnd = new CsEditCodeEditorWindow();
                wnd.Init( d.ProjId, d.DocId, d.FilePathRel );
                wnd.Show();
            } */
        }

        private void OnButtonClick( object sender, RoutedEventArgs e )
        {
            foreach( DocumentInfo d in allDocs ) {
                if ( CsEditWorkspace.Instance.GetIsEditorWindowOpen( d.DocId ) ) continue;

                var wnd = new CsEditCodeEditorWindow();
                wnd.Init( d.ProjId, d.DocId, d.FilePathRel );
                wnd.Show();

                Console.WriteLine( "Opened a new editor window : " + d.DocId.ToString() );
                return;
            }

            Console.WriteLine( "No more documents to open!" );
        }

// https://docs.microsoft.com/en-us/dotnet/api/system.componentmodel.canceleventargs?view=net-6.0 
        private void OnClosing( object sender, CancelEventArgs e )
        {
            Console.WriteLine( "CLOSING A WINDOW..." );
            //e.Cancel = false;
        }


    }
}
