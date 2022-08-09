using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using Avalonia.Interactivity; // RoutedEventArgs
using Avalonia.Input; // KeyEventArgs

using Avalonia.Media; // SolidColorBrush

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
        private ProjectId _projectId;
        private DocumentId _documentId;

        private RoslynCodeEditor _editorControl;

        public CsEditCodeEditorWindow()
        {
            InitializeComponent();

Focusable = true;

            Closing += OnClosing;
#if DEBUG
            this.AttachDevTools();
#endif

// "size" is of type (Avalonia.Controls.)Size?
// https://stackoverflow.com/questions/70338154/avalonia-keep-window-position-after-resize
            ClientSizeProperty.Changed.Subscribe( size =>
            {
                // http://reference.avaloniaui.net/api/Avalonia/AvaloniaPropertyChangedEventArgs/ 
                if ( size.Sender != this ) {
                    //Console.WriteLine( "ClientSizeProperty - SKIP this event." );
                    return;
                }

                RoslynCodeEditor editorControl = this.FindControl<RoslynCodeEditor>("EditorXX");
                _editorControl = editorControl;

                // set the RoslynCodeEditor size = resized parent size minus margins.

                editorControl.Width = size.NewValue.Value.Width - 10;
                editorControl.Height = size.NewValue.Value.Height - 10;
            });

            //Console.WriteLine( "Screens.ScreenCount = " + Screens.ScreenCount );

            // at linux, multiple screens appear as one big screen here.
            // => usually multiple screens are arranged side-by-side.
            // => the height value is more reliable.

            int screenHeight = Screens.Primary.WorkingArea.Height;

            Width = (int) ( screenHeight * 1.20 );
            Height = (int) ( screenHeight * 0.80 );
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void Init( ProjectId projId, DocumentId docId, string filePath ) {

            _projectId = projId;
            _documentId = docId;

            Title = filePath;

            RoslynHost host = CsEditWorkspace.Instance.GetRoslynHost();
            DocumentViewModel dvm = new DocumentViewModel( host, null );

            Console.WriteLine( "looking for the editor control..." );
            RoslynCodeEditor editorControl = this.FindControl<RoslynCodeEditor>("EditorXX");

            // 20220709 change the default text color, when no specific syntax highlighting is used.
            // => in FluentTheme "Light" mode the default color is too light, making text poorly readable.
            editorControl.Foreground = new SolidColorBrush( Color.FromRgb( 0x40, 0x40, 0x40 ) ); // default is D4.

            MenuItem menuItem_goToDefinition = new MenuItem { Header = "Go to Definition" };
            menuItem_goToDefinition.Click += OnMenuClick_goToDefinition;

            editorControl.ContextMenu = new ContextMenu
            {
                Items = new List<MenuItem>
                {
                    // TODO these would be nice BUT ARE NOT WORKING CORRECTLY. no command specified here?
                    //new MenuItem { Header = "Copy", InputGesture = new KeyGesture(Key.C, KeyModifiers.Control) },
                    //new MenuItem { Header = "Paste", InputGesture = new KeyGesture(Key.V, KeyModifiers.Control) },
                    //new MenuItem { Header = "Cut", InputGesture = new KeyGesture(Key.X, KeyModifiers.Control) },
                    //null, // separator

// see src/RoslynPad.Editor.Shared/CodeTextEditor.cs around line 70:
                    //new MenuItem { Header = "Copy", Command = AvaloniaEdit.ApplicationCommands.Copy },
                    //new MenuItem { Header = "Paste", Command = AvaloniaEdit.ApplicationCommands.Paste },
                    //new MenuItem { Header = "Cut", Command = AvaloniaEdit.ApplicationCommands.Cut },
                    //new MenuItem { Header = "FindNext", Command = AvaloniaEdit.Search.SearchCommands.FindNext, InputGesture = new KeyGesture(Key.G, KeyModifiers.Control) },

                    menuItem_goToDefinition
                }
            };

// TODO adding a ContextMenu and using it with a right-click seems to mess up the text selection somehow???
// => it does not matter whether there is a handler for the menu item or not (so cannot fix it there?).
// => a workaround is to select a small stretch of text, and to use the ContextMenu only after that.

            //editorControl.TextArea.RightClickMovesCaret = false; // TODO the latest versions have this???

            editorControl.TextArea.Caret.PositionChanged += Caret_PositionChanged;

            editorControl.DataContext = dvm;

            OnItemLoaded( editorControl, docId) ;
        }



        public void SelectAndShowTextSpan( int start, int length ) {

            // first make sure that the new selection will be visible.
            // NOTICE the methods like TextEditor.ScollTo(line,column) are not working?

            _editorControl.TextArea.Caret.Offset = start;
            _editorControl.TextArea.Caret.BringCaretToView();

            // then make the actual selection.

            _editorControl.Select( start, length );

            if ( IsActive == false ) {
                // it seems that this window is not the topmost one,
                // so activate it now...
                ActivateWindow();
            }
        }

        public void ActivateWindow() {

            // there is a method Window.Activate() which is intended to give focus to a window.
            // however, at Linux/XWindow system it depends on the windowmanager functionality, and is often blocked.
            // => but this (store position, hide and show, revert position) is an excellent replacement.

            PixelPoint pos = Position;
            Hide();

            Show();
            Position = pos;
        }



        private void OnMenuClick_goToDefinition(object sender, RoutedEventArgs e)
        {
            RoslynCodeEditor editorControl = this.FindControl<RoslynCodeEditor>("EditorXX");
            int caretPosition = editorControl.CaretOffset;

            CsEditWorkspace.Instance.GoToDefinition( _projectId, _documentId, caretPosition );
        }

        private void Caret_PositionChanged(object sender, EventArgs e)
        {
/* see AvaloniaEditGIT/src/AvaloniaEdit.Demo/MainWindow.xaml
            _statusTextBlock.Text = string.Format("Line {0} Column {1}", 
                _textEditor.TextArea.Caret.Line,
                _textEditor.TextArea.Caret.Column); */
if ( _editorControl == null ) return;
Console.WriteLine( string.Format("Line {0} Column {1}", _editorControl.TextArea.Caret.Line, _editorControl.TextArea.Caret.Column ) );
        }



        private void OnItemLoaded(RoslynCodeEditor editor, DocumentId docId)
        {

            RoslynHost host = CsEditWorkspace.Instance.GetRoslynHost();

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

            IDocumentModificationsTracker tracker = CsEditWorkspace.Instance.GetTracker( docId );
            container.SetTracker( tracker );

            CsEditWorkspace.Instance.SetEditorWindowAsOpened( docId, this, container );

            viewModel.Initialize(documentId);
        }

// https://docs.microsoft.com/en-us/dotnet/api/system.componentmodel.canceleventargs?view=net-6.0 
        private void OnClosing( object sender, CancelEventArgs e )
        {
            Console.WriteLine( "ABOUT TO CLOSE AN EDITOR WINDOW" );
            e.Cancel = false;

            RoslynHost host = CsEditWorkspace.Instance.GetRoslynHost();
            RoslynWorkspace ws = CsEditWorkspace.Instance.GetRoslynWorkspace();

            Console.WriteLine( "looking for the editor control(2)..." );
            RoslynCodeEditor editor = this.FindControl<RoslynCodeEditor>("EditorXX");

            editor.OnClosing( host, ws, _documentId );

            CsEditWorkspace.Instance.SetEditorWindowAsClosed( _documentId );
        }



#region internalclass

        class DocumentViewModel : INotifyPropertyChanged
        {
            private bool _isReadOnly;
            private readonly RoslynHost _host;
            private string _result;

//public int DefaultWidth { get; } = 1700;
//public int DefaultHeight { get; } = 1700;

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
