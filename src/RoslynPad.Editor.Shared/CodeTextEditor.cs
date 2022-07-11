using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
#if AVALONIA
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Highlighting;
using Brush = Avalonia.Media.IBrush;
using MouseEventArgs = Avalonia.Input.PointerEventArgs;
using TextCompositionEventArgs = Avalonia.Input.TextInputEventArgs;
using RoutingStrategy = Avalonia.Interactivity.RoutingStrategies;
using CommandBinding = AvaloniaEdit.RoutedCommandBinding;
using AvalonEditCommands = AvaloniaEdit.AvaloniaEditCommands;
using ModifierKeys = Avalonia.Input.KeyModifiers;
#else
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
#endif

namespace RoslynPad.Editor
{
    public partial class CodeTextEditor : TextEditor
    {
        private CustomCompletionWindow? _completionWindow;
        private OverloadInsightWindow? _insightWindow;
        private ToolTip? _toolTip;

        public CodeTextEditor()
        {
            Options = new TextEditorOptions
            {
                ConvertTabsToSpaces = true,
                //AllowScrollBelowDocument = true,
                AllowScrollBelowDocument = false, // 20220709
                IndentationSize = 4,
                EnableEmailHyperlinks = false,
            };

            TextArea.TextView.VisualLinesChanged += OnVisualLinesChanged;
            TextArea.TextEntering += OnTextEntering;
            TextArea.TextEntered += OnTextEntered;

            var commandBindings = TextArea.CommandBindings;
            var deleteLineCommand = commandBindings.OfType<CommandBinding>().FirstOrDefault(x =>
                x.Command == AvalonEditCommands.DeleteLine);
            if (deleteLineCommand != null)
            {
                commandBindings.Remove(deleteLineCommand);
            }

            var contextMenu = new ContextMenu();
            contextMenu.SetItems(new[]
            {
                new MenuItem { Command = ApplicationCommands.Cut },
                new MenuItem { Command = ApplicationCommands.Copy },
                new MenuItem { Command = ApplicationCommands.Paste }
            });
            ContextMenu = contextMenu;

            Initialize();

            Console.WriteLine( "setup SyntaxHighlighting (using extension .cs)" );
            SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(".cs");

// need to change the colors???
// https://stackoverflow.com/questions/37395626/how-can-i-programmatically-change-the-default-colors-in-avaloneditor-for-c-sharp 
// https://github.com/icsharpcode/AvalonEdit/blob/master/ICSharpCode.AvalonEdit/Highlighting/Resources/CSharp-Mode.xshd 

//var commentHighlighting = SyntaxHighlighting.NamedHighlightingColors.First( c => c.Name == "Comment" );
//commentHighlighting.Foreground = new SimpleHighlightingBrush( Colors.Red ); // Color.FromRgb( 0xC0, 0xC0, 0xC0 )

// also the "default color", when used when no specific rules are applied, needs to be adjusted.
// https://stackoverflow.com/questions/25771201/avalonedit-default-color-when-no-rules-are-matched 
// the trick of changing the editor's "Foreground" property, works but it must be done elsewhere.
// see CsEditCodeEditorWindow.axaml.cs around line 95.

        }

        partial void Initialize();

        public static readonly StyledProperty<Brush> CompletionBackgroundProperty = CommonProperty.Register<CodeTextEditor, Brush>(
            nameof(CompletionBackground), CreateDefaultCompletionBackground());

        public bool IsCompletionWindowOpen => _completionWindow?.IsVisible == true;

        public void CloseCompletionWindow()
        {
            if (_completionWindow != null)
            {
                _completionWindow.Close();
                _completionWindow = null;
            }
        }

        public bool IsInsightWindowOpen => _insightWindow?.IsVisible == true;

        public void CloseInsightWindow()
        {
            if (_insightWindow != null)
            {
                _insightWindow.Close();
                _insightWindow = null;
            }
        }

        private static Brush CreateDefaultCompletionBackground()
        {
            return new SolidColorBrush(Color.FromRgb(240, 240, 240)).AsFrozen();
        }

        public Brush CompletionBackground
        {
            get => this.GetValue(CompletionBackgroundProperty);
            set => this.SetValue(CompletionBackgroundProperty, value);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {

// most of the keypresses do NOT reach this stage. for example:
//^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
// CTRL-Z => undo
// CTRL-F => find
// CTRL-X => cut
// CTRL-C => copy
// CTRL-V => paste
Console.WriteLine( "CET.OnKeyDown() : " + e.Key + " " + e.HasModifiers(ModifierKeys.Control) );

            base.OnKeyDown(e);

// CTRL-space will open the completions-dialog...
            if (e.Key == Key.Space && e.HasModifiers(ModifierKeys.Control))
            {
                e.Handled = true;
                var mode = e.HasModifiers(ModifierKeys.Shift)
                    ? TriggerMode.SignatureHelp
                    : TriggerMode.Completion;
                _ = ShowCompletion(mode);
            }

// TODO how to set CTRL-G as an alias to "F3" (find next)???
// TODO how to set CTRL-G as an alias to "F3" (find next)???
// TODO how to set CTRL-G as an alias to "F3" (find next)???
            if (e.Key == Key.G && e.HasModifiers(ModifierKeys.Control))
            {

// there should exist: SearchPanel TextEditor.SearchPanel ???
// => 20220616 git-version at least has it (and a corresponding private field).



Console.WriteLine( "findnext execute..." ); // THE BELOW STUFF IS NOT YET WORKING...
//Console.WriteLine( "findnext CANexecute=" + AvaloniaEdit.Search.SearchCommands.FindNext.CanExecute( null, null ) );
//Console.WriteLine( "findnext CANexecute=" + base.SearchPanel.FindNext() );
//AvaloniaEdit.Search.SearchCommands.FindNext.Execute( null, null );
//AvaloniaEdit.Search.SearchCommands.FindNext.Execute( this, e );
            }
        }

        private enum TriggerMode
        {
            Text,
            Completion,
            SignatureHelp
        }

        public static readonly RoutedEvent ToolTipRequestEvent = CommonEvent.Register<CodeTextEditor, ToolTipRequestEventArgs>(
            nameof(ToolTipRequest), RoutingStrategy.Bubble);

        public Func<ToolTipRequestEventArgs, Task>? AsyncToolTipRequest { get; set; }

        public event EventHandler<ToolTipRequestEventArgs> ToolTipRequest
        {
            add => AddHandler(ToolTipRequestEvent, value);
            remove => RemoveHandler(ToolTipRequestEvent, value);
        }

        private void OnVisualLinesChanged(object? sender, EventArgs e)
        {
            _toolTip?.Close(this);
        }

        private void OnMouseHoverStopped(object? sender, MouseEventArgs e)
        {
            if (_toolTip != null)
            {
                _toolTip.Close(this);
                e.Handled = true;
            }
        }

        private async void OnMouseHover(object sender, MouseEventArgs e)
        {
            TextViewPosition? position;
            try
            {
                position = TextArea.TextView.GetPositionFloor(e.GetPosition(TextArea.TextView) + TextArea.TextView.ScrollOffset);
            }
            catch (ArgumentOutOfRangeException)
            {
                // TODO: check why this happens
                e.Handled = true;
                return;
            }
            var args = new ToolTipRequestEventArgs { InDocument = position.HasValue };
            if (!position.HasValue || position.Value.Location.IsEmpty)
            {
                return;
            }

            args.LogicalPosition = position.Value.Location;
            args.Position = Document.GetOffset(position.Value.Line, position.Value.Column);

            RaiseEvent(args);

            if (args.ContentToShow == null)
            {
                var asyncRequest = AsyncToolTipRequest?.Invoke(args);
                if (asyncRequest != null)
                {
                    await asyncRequest.ConfigureAwait(true);
                }
            }

            if (args.ContentToShow == null)
            {
                return;
            }

            if (_toolTip == null)
            {
                _toolTip = new ToolTip { MaxWidth = 400 };
                InitializeToolTip();
            }

            if (args.ContentToShow is string stringContent)
            {
                _toolTip.SetContent(this, new TextBlock
                {
                    Text = stringContent,
                    TextWrapping = TextWrapping.Wrap
                });
            }
            else
            {
                _toolTip.SetContent(this, new ContentPresenter
                {
                    Content = args.ContentToShow,
                    MaxWidth = 400
                });
            }

            e.Handled = true;
            _toolTip.Open(this);

            AfterToolTipOpen();
        }

        partial void InitializeToolTip();
        partial void AfterToolTipOpen();

        #region Open & Save File

        public void OpenFile(string fileName)
        {
            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException(fileName);
            }

            _completionWindow?.Close();
            _insightWindow?.Close();

            Load(fileName);
            Document.FileName = fileName;

            SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(Path.GetExtension(fileName));
        }

        public bool SaveFile()
        {
            if (string.IsNullOrEmpty(Document.FileName))
            {
                return false;
            }

            Save(Document.FileName);
            return true;
        }

        #endregion

        #region Code Completion

        public ICodeEditorCompletionProvider? CompletionProvider { get; set; }

        private void OnTextEntered(object sender, TextCompositionEventArgs e)
        {
            _ = ShowCompletion(TriggerMode.Text);
        }

        private async Task ShowCompletion(TriggerMode triggerMode)
        {
            if (CompletionProvider == null)
            {
                return;
            }

            GetCompletionDocument(out var offset);
            var completionChar = triggerMode == TriggerMode.Text ? Document.GetCharAt(offset - 1) : (char?)null;
            var results = await CompletionProvider.GetCompletionData(offset, completionChar,
                        triggerMode == TriggerMode.SignatureHelp).ConfigureAwait(true);
            if (results.OverloadProvider != null)
            {
                results.OverloadProvider.Refresh();

                if (_insightWindow != null && _insightWindow.IsOpen())
                {
                    _insightWindow.Provider = results.OverloadProvider;
                }
                else
                {
                    _insightWindow = new OverloadInsightWindow(TextArea)
                    {
                        Provider = results.OverloadProvider,
                        //Background = CompletionBackground,

                        // 20220617 added here a similar fix as in CodeTextEditor.CustomCompletionWindow.Initialize().
                        // => this will align the OverloadInsightWindow just below the edited section of the text.

                        PlacementTarget = TextArea.TextView,
                        PlacementMode = PlacementMode.Pointer,

                    };

                    // is this method missing? only a "partial" declation exists?
                    InitializeInsightWindow();

                    _insightWindow.Closed += (o, args) => _insightWindow = null;
                    _insightWindow.Show();
                }
                return;
            }

            if (_completionWindow?.IsOpen() != true && results.CompletionData != null && results.CompletionData.Any())
            {
                _insightWindow?.Close();

                // Open code completion after the user has pressed dot:
                _completionWindow = new CustomCompletionWindow(TextArea)
                {
                    MinWidth = 300,
                    CloseWhenCaretAtBeginning = triggerMode == TriggerMode.Completion || triggerMode == TriggerMode.Text,
                    UseHardSelection = results.UseHardSelection,
                };

                InitializeCompletionWindow();

                if (completionChar != null && char.IsLetterOrDigit(completionChar.Value))
                {
                    _completionWindow.StartOffset -= 1;
                }

                var data = _completionWindow.CompletionList.CompletionData;
                ICompletionDataEx? selected = null;
                foreach (var completion in results.CompletionData)
                {
                    if (completion.IsSelected)
                    {
                        selected = completion;
                    }

                    data.Add(completion);
                }

                try
                {
                    _completionWindow.CompletionList.SelectedItem = selected;
                }
                catch (Exception)
                {
                    // TODO-AV: Fix this in AvaloniaEdit
                }

                _completionWindow.Closed += (o, args) => { _completionWindow = null; };
                _completionWindow.Show();
            }
        }

        partial void InitializeInsightWindow();

        partial void InitializeCompletionWindow();

        private void OnTextEntering(object sender, TextCompositionEventArgs args)
        {
            if (args.Text?.Length > 0 && _completionWindow != null)
            {
                if (!IsCharIdentifier(args.Text[0]))
                {
                    // Whenever no identifier letter is typed while the completion window is open,
                    // insert the currently selected element.
                    _completionWindow.CompletionList.RequestInsertion(args);
                }
            }
            // Do not set e.Handled=true.
            // We still want to insert the character that was typed.
        }
        /// <summary>
        /// Checks if a provided char is a well-known identifier
        /// </summary>
        /// <param name="c">The charcater to check</param>
        /// <returns><c>true</c> if <paramref name="c"/> is a well-known identifier.</returns>
        private bool IsCharIdentifier(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }
        /// <summary>
        /// Gets the document used for code completion, can be overridden to provide a custom document
        /// </summary>
        /// <param name="offset"></param>
        /// <returns>The document of this text editor.</returns>
        protected virtual IDocument GetCompletionDocument(out int offset)
        {
            offset = CaretOffset;
            return Document;
        }

        #endregion

        private partial class CustomCompletionWindow : CompletionWindow
        {
            private bool _isSoftSelectionActive;
            private KeyEventArgs? _keyDownArgs;

            public CustomCompletionWindow(TextArea textArea) : base(textArea)
            {
                _isSoftSelectionActive = true;
                CompletionList.SelectionChanged += CompletionListOnSelectionChanged;

                Initialize();
            }

            partial void Initialize();

            private void CompletionListOnSelectionChanged(object sender, SelectionChangedEventArgs args)
            {
                if (!UseHardSelection &&
                    _isSoftSelectionActive && _keyDownArgs?.Handled != true
                    && args.AddedItems?.Count > 0)
                {
                    CompletionList.SelectedItem = null;
                }
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.Key == Key.Home || e.Key == Key.End) return;

                _keyDownArgs = e;

                base.OnKeyDown(e);

                SetSoftSelection(e);
            }

            private void SetSoftSelection(RoutedEventArgs e)
            {
                if (e.Handled)
                {
                    _isSoftSelectionActive = false;
                }
            }

            public bool UseHardSelection { get; set; }
        }
    }
}
