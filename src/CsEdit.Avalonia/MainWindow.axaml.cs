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
        private Dictionary<DocumentId,string> allDocs = null;

        public MainWindow()
        {
            InitializeComponent();
            Closing += OnClosing;

            // this is where CsEditWorkspace will be initialized...
            allDocs = CsEditWorkspace.Instance.GetAllDocuments();
            Console.WriteLine( "allDocs.Count = " + allDocs.Count );

            /* open all windows...
            foreach( KeyValuePair<DocumentId,string> entry in allDocs ) {
                var wnd = new CsEditCodeEditorWindow();
                wnd.Init( entry.Key, entry.Value );
                wnd.Show();
            } */
        }

        private void OnButtonClick( object sender, RoutedEventArgs e )
        {
            foreach( KeyValuePair<DocumentId,string> entry in allDocs ) {
                if ( CsEditWorkspace.Instance.GetIsEditorWindowOpen( entry.Key ) ) continue;

                var wnd = new CsEditCodeEditorWindow();
                wnd.Init( entry.Key, entry.Value );
                wnd.Show();

                Console.WriteLine( "Opened a new editor window : " + entry.Key.ToString() );
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
