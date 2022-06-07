using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using Avalonia.Interactivity; // RoutedEventArgs
using Avalonia.Input; // KeyEventArgs

using Microsoft.CodeAnalysis; // SourceCodeKind etc
using Microsoft.CodeAnalysis.CSharp; // LanguageVersion etc
using Microsoft.CodeAnalysis.Text; // SourceTextContainer etc

using RoslynPad.Roslyn; // RoslynHost



// might be some unnecessary usings here...
using Microsoft.CodeAnalysis.CSharp.Scripting;
using RoslynPad.Editor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting.Hosting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;



namespace CsEdit.Avalonia
{
    public partial class CsEditCodeEditorWindow : Window
    {
        private DocumentId documentId;

        public CsEditCodeEditorWindow()
        {
            InitializeComponent();
            Closing += OnClosing;
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void Init( DocumentId docId, string filePath ) {

documentId = docId;

            Title = filePath;

            RoslynHost host = CsEditWorkspace.Instance.GetHost();
            DocumentViewModel dvm = new DocumentViewModel( host, null );

Console.WriteLine( "looking for the editor control..." );
RoslynCodeEditor eee = this.FindControl<RoslynCodeEditor>("EditorXX");

eee.DataContext = dvm;

OnItemLoaded(eee, docId);

        }



        private void OnItemLoaded(RoslynCodeEditor editor, DocumentId docId)
        {

            RoslynHost host = CsEditWorkspace.Instance.GetHost();


Console.WriteLine( "OnItemLoaded() called!" );

            editor.Focus();

            var viewModel = (DocumentViewModel)editor.DataContext;
            var workingDirectory = Directory.GetCurrentDirectory();

            var previous = viewModel.LastGoodPrevious;
            if (previous != null)
            {
                editor.CreatingDocument += (o, args) =>
                {
                    args.DocumentId = docId;
                };
            }

RoslynWorkspace ws = CsEditWorkspace.Instance.GetRoslynWorkspace();

            string currentText = CsEditWorkspace.Instance.GetCurrentText( docId );

            var documentId = editor.Initialize_alt(host, ws, docId, currentText, new ClassificationHighlightColors(), out AvalonEditTextContainer container );

            CsEditWorkspace.Instance.SetEditorWindowAsOpened( docId, container );

            viewModel.Initialize(documentId);
        }

// https://docs.microsoft.com/en-us/dotnet/api/system.componentmodel.canceleventargs?view=net-6.0 
        private void OnClosing( object sender, CancelEventArgs e )
        {
            Console.WriteLine( "ABOUT TO CLOSE AN EDITOR WINDOW" );
            e.Cancel = false;

            RoslynHost host = CsEditWorkspace.Instance.GetHost();
            RoslynWorkspace ws = CsEditWorkspace.Instance.GetRoslynWorkspace();

Console.WriteLine( "looking for the editor control(2)..." );
RoslynCodeEditor editor = this.FindControl<RoslynCodeEditor>("EditorXX");

            editor.OnClosing( host, ws, documentId );

            CsEditWorkspace.Instance.SetEditorWindowAsClosed( documentId );
        }



#region internalclass

        class DocumentViewModel : INotifyPropertyChanged
        {
            private bool _isReadOnly;
            private readonly RoslynHost _host;
            private string _result;

            public DocumentViewModel(RoslynHost host, DocumentViewModel previous)
            {
                _host = host;
                Previous = previous;
            }

            internal void Initialize(DocumentId id)
            {
                Id = id;
            }

            public DocumentId Id { get; private set; }

            public bool IsReadOnly
            {
                get { return _isReadOnly; }
                private set { SetProperty(ref _isReadOnly, value); }
            }

            public DocumentViewModel Previous { get; }

            public DocumentViewModel LastGoodPrevious
            {
                get
                {
                    var previous = Previous;

                    while (previous != null && previous.HasError)
                    {
                        previous = previous.Previous;
                    }

                    return previous;
                }
            }

            public Script<object> Script { get; private set; }

            public string Text { get; set; }

            public bool HasError { get; private set; }

            public string Result
            {
                get { return _result; }
                private set { SetProperty(ref _result, value); }
            }

            private static MethodInfo HasSubmissionResult { get; } =
                typeof(Compilation).GetMethod(nameof(HasSubmissionResult), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            private static PrintOptions PrintOptions { get; } = 
                new PrintOptions { MemberDisplayFormat = MemberDisplayFormat.SeparateLines };

            public async Task<bool> TrySubmit()
            {
                Result = null;

                Script = LastGoodPrevious?.Script.ContinueWith(Text) ??
                    CSharpScript.Create(Text, ScriptOptions.Default
                        .WithReferences(_host.DefaultReferences)
                        .WithImports(_host.DefaultImports));

                var compilation = Script.GetCompilation();
                var hasResult = (bool)HasSubmissionResult.Invoke(compilation, null);
                var diagnostics = Script.Compile();
                if (diagnostics.Any(t => t.Severity == DiagnosticSeverity.Error))
                {
                    Result = string.Join(Environment.NewLine, diagnostics.Select(FormatObject));
                    return false;
                }

                IsReadOnly = true;

                //await Execute(hasResult);
                await Execute(hasResult).ConfigureAwait(false);

                return true;
            }

            private async Task Execute(bool hasResult)
            {
                try
                {
                    //var result = await Script.RunAsync();
                    var result = await Script.RunAsync().ConfigureAwait(false);

                    if (result.Exception != null)
                    {
                        HasError = true;
                        Result = FormatException(result.Exception);
                    }
                    else
                    {
                        Result = hasResult ? FormatObject(result.ReturnValue) : null;
                    }
                }
                catch (Exception ex)
                {
                    HasError = true;
                    Result = FormatException(ex);
                }
            }

            private static string FormatException(Exception ex)
            {
                return CSharpObjectFormatter.Instance.FormatException(ex);
            }

            private static string FormatObject(object o)
            {
                return CSharpObjectFormatter.Instance.FormatObject(o, PrintOptions);
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
            {
                if (!EqualityComparer<T>.Default.Equals(field, value))
                {
                    field = value;
                    OnPropertyChanged(propertyName);
                    return true;
                }
                return false;
            }

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

#endregion // internalclass

    }
}
