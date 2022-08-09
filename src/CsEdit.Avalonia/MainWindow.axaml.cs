using Avalonia.Controls;
using Avalonia.Interactivity; // RoutedEventArgs

using Microsoft.CodeAnalysis; // SourceCodeKind etc
using Microsoft.CodeAnalysis.CSharp; // LanguageVersion etc

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; // ObservableCollection
using System.ComponentModel; // CancelEventArgs
using System.Linq; // for Dictionary.ElementAt()...

namespace CsEdit.Avalonia
{
    public partial class MainWindow : Window
    {
        // we use this class as a singleton in practice,
        // but do not have private constructor etc formal stuff here.
        public static MainWindow Instance { get; private set; } = null;

        public MainWindow()
        {
            if ( Instance != null ) { 
                throw new InvalidOperationException( "MainWindow singleton-instance already exists!" );
            }

            Instance = this;

            InitializeComponent();
            Closing += OnClosing;
        }

        private void OnButtonClick( object sender, RoutedEventArgs e )
        {
            // we can get the button control as the "sender" parameter.
            Button b = (sender) as Button;

            string cmdParam = "";
            if ( b.CommandParameter != null ) cmdParam = b.CommandParameter.ToString();

            Console.WriteLine( "cmdparam=" + cmdParam );

            // the button control is a special case in that sense, that it is generated by the treeview.
            // it still seems to have the "x:Name" and other properties as usually, but it seems that
            // the standard "FindControl<T>()" method won't find it.
            // => if course it's lifecycle is managed by the treeview implementation (so we may not delete it etc).
            // => but still we would like to have access to it, to modify properties like color, text content etc...
            // => it seems that we must use "Avalonia.VisualTree/VisualExtensions" to find the control.
            // => see the MainWindowViewModel.FindSubControl<T>() method.

            ((MainWindowViewModel)(this.DataContext)).CreateOrShowEditorWindow( cmdParam, null );
        }

// https://docs.microsoft.com/en-us/dotnet/api/system.componentmodel.canceleventargs?view=net-6.0 
        private void OnClosing( object sender, CancelEventArgs e )
        {
            Console.WriteLine( "CLOSING A WINDOW..." );
            //e.Cancel = false;
        }



    }
}
