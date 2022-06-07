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
        private int EditorWindowCounter = 0;

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
            Console.WriteLine( "Open a new editor window." );

            KeyValuePair<DocumentId,string> entry = allDocs.ElementAt( EditorWindowCounter % allDocs.Count );
            var wnd = new CsEditCodeEditorWindow();
            wnd.Init( entry.Key, entry.Value );
            wnd.Show();

            EditorWindowCounter++;
        }

// https://docs.microsoft.com/en-us/dotnet/api/system.componentmodel.canceleventargs?view=net-6.0 
        private void OnClosing( object sender, CancelEventArgs e )
        {
            Console.WriteLine( "CLOSING A WINDOW..." );
            //e.Cancel = false;
        }


    }
}
