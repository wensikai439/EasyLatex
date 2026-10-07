using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using EasyLatex.Core;
using EasyLatex.EditorSupport;
using EasyLatex.Models;
using EasyLatex.Services;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.Win32;

namespace EasyLatex;

public partial class MainWindow : Window
{
    private AppSettings _settings = new();
    private readonly ObservableCollection<DocumentSession> _documents = [];
    public ObservableCollection<PdfPageModel> Pages { get; } = [];
    private DocumentSession? _active;
    private readonly CompilerService _compiler = new();
    private readonly PdfService _pdf = new();
    private readonly SyncTexService _syncTex = new();
    private CancellationTokenSource? _buildCancel, _aiCancel, _pdfCancel;
    private readonly DispatcherTimer _analysisTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly DispatcherTimer _autoTimer = new() { Interval = TimeSpan.FromMilliseconds(1600) };
    private readonly DispatcherTimer _recoveryTimer = new() { Interval = TimeSpan.FromSeconds(8) };
    private FoldingManager? _folding;
    private CompletionWindow? _completion;
    private string? _projectRoot;
    private string _lastLog = "";
    private bool _initialized, _building, _focus, _pendingAuto;
    private bool _verificationMode;
    internal void EnableVerificationMode() => _verificationMode = true;
    private double _zoom = 1;
    private bool _fitWidth = true;
    private int _pdfGeneration;
    private bool _pdfRenderScheduled;
    private readonly Dictionary<PdfPageModel, Task> _rendering = [];
    private readonly Queue<PdfPageModel> _renderCache = new();
    private (DocumentSession Doc, string Source, int Offset, string Result)? _proposal;
    private record ProjectFile(string Path, string Label);
    private record RecoveryFile(string? Path, string Text);

    public MainWindow()
    {
        InitializeComponent(); DataContext = this;
        Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/EasyLatex;component/Resources/EasyLatex.png"));
        _settings = SettingsStore.Load(); Width = Math.Clamp(_settings.WindowWidth, 960, 2200); Height = Math.Clamp(_settings.WindowHeight, 620, 1600);
        DocumentTabs.ItemsSource = _documents;
        using var reader = XmlReader.Create(Application.GetResourceStream(new Uri("/EasyLatex;component/Resources/LaTeX.xshd", UriKind.Relative))!.Stream);
        Editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        Editor.Options.ConvertTabsToSpaces = true; Editor.Options.IndentationSize = 2; Editor.Options.EnableHyperlinks = false; Editor.Options.EnableEmailHyperlinks = false;
        Editor.TextArea.TextEntered += Editor_TextEntered;
        Editor.TextArea.TextEntering += (_, e) =>
        {
            if (e.Text == "}" && Editor.SelectionLength == 0 && Editor.CaretOffset < Editor.Document.TextLength && Editor.Document.GetCharAt(Editor.CaretOffset) == '}')
            { Editor.CaretOffset++; e.Handled = true; }
        };
        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateCaret();
        Editor.TextArea.SelectionChanged += (_, _) => UpdateAiScope();
        _analysisTimer.Tick += (_, _) => { _analysisTimer.Stop(); UpdateOutline(); };
        _autoTimer.Tick += async (_, _) => { _autoTimer.Stop(); if (_building) _pendingAuto = true; else if (_settings.AutoCompile && _active?.FilePath is not null) await CompileAsync(); };
        _recoveryTimer.Tick += (_, _) => WriteRecovery();
        _initialized = true;
        ApplySettings(); RefreshRecents();
        AddDocument(new(Templates.Article));
        Loaded += (_, _) => { RestoreRecovery(); _recoveryTimer.Start(); };
        PreviewKeyDown += Window_PreviewKeyDown;
    }

    private void ApplySettings()
    {
        Editor.FontSize = Math.Clamp(_settings.EditorFontSize, 11, 24); AutoCompile.IsChecked = _settings.AutoCompile;
        var resources = Application.Current.Resources;
        foreach (var (key, light, dark) in new[] { ("Surface", "#FFFFFF", "#20222B"), ("Canvas", "#F7F7FA", "#191B23"), ("Ink", "#252733", "#E4E5EE"), ("Muted", "#767A8C", "#999FAF"), ("Line", "#E8E8EF", "#353846"), ("Accent", "#6854CC", "#A797F4"), ("AccentSoft", "#EFECFB", "#363044"), ("PrimaryInk", "#FFFFFF", "#251F40") })
            resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.DarkMode ? dark : light));
        EngineStatus.Text = _settings.Engine == EngineKind.Auto ? "自动引擎" : _settings.Engine.ToString();
        foreach (var (name, light, dark) in new[] { ("Comment", "#8B91A3", "#929BAE"), ("Command", "#7657BD", "#BDACF9"), ("Math", "#278C88", "#6ACCC4"), ("Bracket", "#D47B37", "#EBBC86") })
            if (Editor.SyntaxHighlighting?.GetNamedColor(name) is { } color) color.Foreground = new SimpleHighlightingBrush((Color)ColorConverter.ConvertFromString(_settings.DarkMode ? dark : light));
        Editor.TextArea.SelectionBrush = (Brush)resources["AccentSoft"]; Editor.TextArea.SelectionForeground = (Brush)resources["Ink"];
        Editor.TextArea.TextView.Redraw();
        if (CompilerService.Detect(_settings.CompilerDirectory).Count == 0) StatusText.Text = "未发现编译引擎 · 打开设置安装轻量引擎即可开始";
    }

    private void AddDocument(DocumentSession document)
    {
        _documents.Add(document); SwitchTo(document);
    }
    private void SwitchTo(DocumentSession document)
    {
        if (_folding is not null) { FoldingManager.Uninstall(_folding); _folding = null; }
        _active = document; Editor.Document = document.Document; _folding = FoldingManager.Install(Editor.TextArea);
        foreach (var tab in _documents) tab.IsActive = tab == document;
        Title = $"{document.Title} — EasyLatex"; UpdateOutline(); UpdateCaret(); UpdateAiScope(); Editor.Focus();
    }
    public void OpenPath(string path)
    {
        try
        {
            path = Path.GetFullPath(path);
            var existing = _documents.FirstOrDefault(d => string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) { SwitchTo(existing); return; }
            var document = DocumentSession.Load(path);
            if (_documents.Count == 1 && _documents[0].FilePath is null && !_documents[0].IsDirty && _documents[0].Document.Text == Templates.Article) _documents.Clear();
            AddDocument(document);
            if (_projectRoot is null || !path.StartsWith(_projectRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) _projectRoot = Path.GetDirectoryName(path);
            RefreshProject(); AddRecent(path);
            StatusText.Text = "已打开 " + Path.GetFileName(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { Notify(ex.Message); }
    }
    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "LaTeX 项目文件|*.tex;*.bib;*.sty;*.cls|所有文件|*.*", Multiselect = true };
        if (dialog.ShowDialog(this) == true) foreach (var file in dialog.FileNames) OpenPath(file);
    }
    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "打开 LaTeX 项目文件夹" };
        if (dialog.ShowDialog(this) != true) return;
        _projectRoot = dialog.FolderName; RefreshProject();
        var main = Directory.GetFiles(_projectRoot, "*.tex").OrderBy(p => Path.GetFileName(p).Equals("main.tex", StringComparison.OrdinalIgnoreCase) ? 0 : 1).FirstOrDefault();
        if (main is not null) OpenPath(main);
    }
    private void New_Click(object sender, RoutedEventArgs e) => AddDocument(new(Templates.Article));
    private void ChineseTemplate_Click(object sender, RoutedEventArgs e) => AddDocument(new(Templates.Chinese));
    private void BeamerTemplate_Click(object sender, RoutedEventArgs e) => AddDocument(new(Templates.Beamer));
    private bool SaveDocument(DocumentSession document, bool saveAs = false)
    {
        try
        {
            if (document.FilePath is null || saveAs)
            {
                var dialog = new SaveFileDialog { Filter = "LaTeX 文档|*.tex|BibTeX|*.bib|所有文件|*.*", FileName = document.FilePath is null ? "main.tex" : Path.GetFileName(document.FilePath), DefaultExt = ".tex" };
                if (dialog.ShowDialog(this) != true) return false;
                if (_documents.Any(d => d != document && string.Equals(d.FilePath, dialog.FileName, StringComparison.OrdinalIgnoreCase))) { Notify("这个文件已在其他标签页打开。"); return false; }
                document.FilePath = dialog.FileName;
            }
            document.Save(); AddRecent(document.FilePath!); _projectRoot ??= Path.GetDirectoryName(document.FilePath); RefreshProject();
            Title = $"{document.Title} — EasyLatex"; StatusText.Text = "已保存 " + Path.GetFileName(document.FilePath); return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.EncoderFallbackException) { Notify(ex.Message); return false; }
    }
    private void Save_Click(object sender, RoutedEventArgs e) { if (_active is not null) SaveDocument(_active); }
    private void SaveAs_Click(object sender, RoutedEventArgs e) { if (_active is not null) SaveDocument(_active, true); }
    private void Tab_Click(object sender, RoutedEventArgs e) { if (((Button)sender).Tag is DocumentSession document) SwitchTo(document); }
    private bool ConfirmClose(DocumentSession document)
    {
        if (!document.IsDirty) return true;
        var answer = MessageBox.Show(this, $"保存对“{document.Title.TrimEnd(' ', '•')}”的修改？", "关闭文档", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return answer == MessageBoxResult.No || answer == MessageBoxResult.Yes && SaveDocument(document);
    }
    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        var document = (DocumentSession)((Button)sender).Tag;
        if (!ConfirmClose(document)) return;
        var index = _documents.IndexOf(document); _documents.Remove(document);
        if (_active == document) { if (_documents.Count > 0) SwitchTo(_documents[Math.Min(index, _documents.Count - 1)]); else AddDocument(new("")); }
    }
    private void AddRecent(string path)
    {
        _settings.RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        _settings.RecentFiles.Insert(0, path); _settings.RecentFiles = _settings.RecentFiles.Take(10).ToList(); SettingsStore.Save(_settings); RefreshRecents();
    }
    private void RefreshRecents()
    {
        RecentMenu.Items.Clear();
        foreach (var path in _settings.RecentFiles.Where(File.Exists)) { var item = new MenuItem { Header = Path.GetFileName(path), ToolTip = path }; item.Click += (_, _) => OpenPath(path); RecentMenu.Items.Add(item); }
        RecentMenu.IsEnabled = RecentMenu.Items.Count > 0;
    }

    private void Editor_TextChanged(object sender, EventArgs e)
    {
        if (!_initialized || _active is null) return;
        Title = $"{_active.Title} — EasyLatex"; _analysisTimer.Stop(); _analysisTimer.Start(); UpdateAiScope();
        if (_settings.AutoCompile && _active.FilePath is not null) { _autoTimer.Stop(); _autoTimer.Start(); }
    }
    private void UpdateOutline()
    {
        if (_active is null) return;
        OutlineList.ItemsSource = LatexParser.GetOutline(Editor.Text);
        _folding?.UpdateFoldings(LatexFolding.Get(Editor.Document), -1);
    }
    private void UpdateCaret() => CaretStatus.Text = $"第 {Editor.TextArea.Caret.Line} 行，第 {Editor.TextArea.Caret.Column} 列 · {_active?.Encoding.WebName ?? "utf-8"}";
    private void Outline_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (OutlineList.SelectedItem is OutlineEntry entry) JumpToLine(entry.Line); }
    private void JumpToLine(int line)
    {
        line = Math.Clamp(line, 1, Editor.Document.LineCount); Editor.TextArea.Caret.Line = line; Editor.ScrollToLine(line); Editor.Focus();
    }
    private void RefreshProject()
    {
        if (_projectRoot is null || !Directory.Exists(_projectRoot)) return;
        var files = new List<ProjectFile>();
        void Visit(string dir, int depth)
        {
            if (depth > 4 || files.Count >= 500) return;
            try
            {
                foreach (var path in Directory.GetFiles(dir).Where(p => new[] { ".tex", ".bib", ".sty", ".cls" }.Contains(Path.GetExtension(p).ToLowerInvariant()))) files.Add(new(path, Path.GetRelativePath(_projectRoot, path)));
                foreach (var sub in Directory.GetDirectories(dir).Where(p => !Path.GetFileName(p).StartsWith('.') && (File.GetAttributes(p) & FileAttributes.ReparsePoint) == 0)) Visit(sub, depth + 1);
            }
            catch (UnauthorizedAccessException) { }
        }
        Visit(_projectRoot, 0); ProjectList.ItemsSource = files.OrderBy(p => p.Label).ToList();
    }
    private void Project_DoubleClick(object sender, MouseButtonEventArgs e) { if (ProjectList.SelectedItem is ProjectFile file) OpenPath(file.Path); }
    private void RefreshProject_Click(object sender, RoutedEventArgs e) => RefreshProject();

    private async void Compile_Click(object sender, RoutedEventArgs e) { if (_building) _buildCancel?.Cancel(); else await CompileAsync(); }
    public async Task CompileAsync()
    {
        if (_building || _active is null) return;
        if (!SaveDocument(_active)) return;
        foreach (var doc in _documents.Where(d => d.IsDirty && d.FilePath is not null).ToList()) if (!SaveDocument(doc)) return;
        var source = _active;
        string master;
        try { master = LatexParser.ResolveMaster(source.FilePath!, source.Document.Text, _projectRoot); }
        catch (IOException ex) { Notify(ex.Message); return; }
        if (!File.Exists(master)) { Notify("主文件不存在：" + master); return; }
        _autoTimer.Stop(); _building = true; _buildCancel = new(); CompileButton.Content = "取消编译"; StatusText.Text = "正在编译…";
        try
        {
            var result = await _compiler.BuildAsync(master, _settings, line => Dispatcher.BeginInvoke(() => { if (line.Contains("Downloading") || line.Contains("download", StringComparison.OrdinalIgnoreCase)) StatusText.Text = "正在获取所需宏包…"; }), _buildCancel.Token);
            _lastLog = result.Output; BuildLog.Text = result.Output; DiagnosticsList.ItemsSource = result.Diagnostics;
            var errors = result.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error); var warnings = result.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
            DiagnosticsTitle.Text = $"编译信息 · {errors} 个错误，{warnings} 个警告";
            if (result.Cancelled) StatusText.Text = "已取消编译 · 上次预览仍保留";
            else if (result.Success && result.PdfPath is { } path)
            {
                await LoadPdfAsync(path); _syncTex.Load(path);
                StatusText.Text = $"编译完成 · {result.Duration.TotalSeconds:F1} 秒"; EngineStatus.Text = result.Engine;
                if (errors == 0 && warnings == 0) DiagnosticsPanel.Visibility = Visibility.Collapsed;
            }
            else { StatusText.Text = "编译未成功 · 上次成功预览仍保留"; DiagnosticsPanel.Visibility = Visibility.Visible; }
        }
        catch (Exception ex) { Notify("编译未完成：" + ex.Message); }
        finally
        {
            _building = false; _buildCancel.Dispose(); _buildCancel = null; CompileButton.Content = "编译  Ctrl+Enter";
            if (_pendingAuto) { _pendingAuto = false; if (_settings.AutoCompile) _autoTimer.Start(); }
        }
    }
    public async Task LoadPdfAsync(string path)
    {
        _pdfCancel?.Cancel(); _pdfCancel?.Dispose(); _pdfCancel = new(); _pdfGeneration++;
        await _pdf.LoadAsync(path); Pages.Clear(); _renderCache.Clear();
        // WinRT exposes PDF dimensions in 96-DPI DIPs; SyncTeX uses 72-DPI PDF points.
        for (var i = 0; i < _pdf.PageCount; i++) { var size = _pdf.Size(i); Pages.Add(new() { Index = i, PointWidth = size.Width * 72 / 96, PointHeight = size.Height * 72 / 96, Width = PageWidth() }); }
        PreviewEmpty.Visibility = Visibility.Collapsed; PreviewLabel.Text = $"PDF · {_pdf.PageCount} 页";
        if (Pages.Count > 0) await RenderPageAsync(Pages[0]);
    }
    private double PageWidth() => _fitWidth ? Math.Max(220, PdfPages.ActualWidth - 48) : 595 * _zoom;
    private void PdfPage_Loaded(object sender, RoutedEventArgs e) => SchedulePdfRender();
    private void PdfPage_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => SchedulePdfRender();
    private void Pdf_ScrollChanged(object sender, ScrollChangedEventArgs e) => SchedulePdfRender();
    private void SchedulePdfRender()
    {
        if (_pdfRenderScheduled) return;
        _pdfRenderScheduled = true;
        Dispatcher.BeginInvoke(() =>
        {
            _pdfRenderScheduled = false;
            foreach (var page in Pages)
                if (PdfPages.ItemContainerGenerator.ContainerFromItem(page) is ListBoxItem { IsVisible: true, ActualHeight: > 0 } item)
                {
                    var top = item.TranslatePoint(new Point(0, 0), PdfPages).Y;
                    if (top + item.ActualHeight > 0 && top < PdfPages.ActualHeight) _ = RenderPageAsync(page);
                }
        }, DispatcherPriority.Loaded);
    }
    private async Task RenderPageAsync(PdfPageModel page)
    {
        if (_pdfCancel is null || page.Image is { } cached && Math.Abs(cached.PixelWidth - Math.Clamp(page.Width * VisualTreeHelper.GetDpi(this).DpiScaleX, 200, 2400)) < 2) return;
        if (_rendering.TryGetValue(page, out var pending)) { await pending; return; }
        var task = RenderPageCoreAsync(page, _pdfGeneration, _pdfCancel.Token);
        _rendering[page] = task;
        try { await task; } finally { _rendering.Remove(page); }
    }
    private async Task RenderPageCoreAsync(PdfPageModel page, int generation, CancellationToken token)
    {
        try
        {
            var dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
            while (generation == _pdfGeneration && !token.IsCancellationRequested && Pages.Contains(page))
            {
                var requestedWidth = page.Width * dpi;
                var image = await _pdf.RenderAsync(page.Index, requestedWidth, token);
                if (generation != _pdfGeneration || token.IsCancellationRequested || image is null) return;
                if (Math.Abs(requestedWidth - page.Width * dpi) > 0.5) continue;
                page.Image = image;
                var previous = _renderCache.Where(p => p != page).ToArray();
                _renderCache.Clear(); foreach (var entry in previous) _renderCache.Enqueue(entry);
                _renderCache.Enqueue(page);
                while (_renderCache.Count > 8) { var old = _renderCache.Dequeue(); if (old != page) old.Image = null; }
                return;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { StatusText.Text = "PDF 预览暂未完成：" + ex.Message; }
    }
    private void Pdf_SizeChanged(object sender, SizeChangedEventArgs e) { if (_fitWidth && _initialized && e.WidthChanged) UpdatePageWidths(); }
    private void UpdatePageWidths()
    {
        foreach (var page in Pages) page.Width = PageWidth();
        ZoomLabel.Content = _fitWidth ? "适合宽度" : $"{_zoom:P0}";
        SchedulePdfRender();
    }
    private void ZoomOut_Click(object sender, RoutedEventArgs e) { _fitWidth = false; _zoom = Math.Max(0.4, _zoom - 0.15); UpdatePageWidths(); }
    private void ZoomIn_Click(object sender, RoutedEventArgs e) { _fitWidth = false; _zoom = Math.Min(3, _zoom + 0.15); UpdatePageWidths(); }
    private void FitWidth_Click(object sender, RoutedEventArgs e) { _fitWidth = true; UpdatePageWidths(); }
    private void Pdf_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        if (e.Delta > 0) ZoomIn_Click(sender, e); else ZoomOut_Click(sender, e); e.Handled = true;
    }
    private void PdfPage_Click(object sender, MouseButtonEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 || ((FrameworkElement)sender).DataContext is not PdfPageModel page) return;
        var at = e.GetPosition((IInputElement)sender);
        var point = _syncTex.Reverse(page.Number, at.X / page.Width * page.PointWidth, at.Y / page.Height * page.PointHeight);
        if (point is null) { StatusText.Text = "此位置没有对应的源码信息"; return; }
        if (File.Exists(point.FilePath)) { OpenPath(point.FilePath); JumpToLine(point.Line); }
        e.Handled = true;
    }
    private void ForwardSearch_Click(object sender, RoutedEventArgs e)
    {
        if (_active?.FilePath is null) return;
        var point = _syncTex.Forward(_active.FilePath, Editor.TextArea.Caret.Line);
        if (point is null || point.Page > Pages.Count) { StatusText.Text = "请先编译，再使用源码与 PDF 定位"; return; }
        var page = Pages[point.Page - 1]; PdfPages.ScrollIntoView(page); PdfPages.SelectedItem = page; StatusText.Text = $"已定位到 PDF 第 {point.Page} 页";
        foreach (var other in Pages) other.Mark(other == page ? point : null);
        PdfPages.UpdateLayout();
        if (PdfPages.ItemContainerGenerator.ContainerFromItem(page) is FrameworkElement item && FindDescendant<ScrollViewer>(PdfPages) is { } scroll)
        {
            var at = item.TranslatePoint(new Point(0, 0), PdfPages);
            scroll.ScrollToVerticalOffset(Math.Max(0, scroll.VerticalOffset + at.Y + page.MarkerTop + 12 - scroll.ViewportHeight * 0.35));
        }
    }
    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_pdf.Path is null) { StatusText.Text = "请先成功编译文档，再导出 PDF"; return; }
        var dialog = new SaveFileDialog { Filter = "PDF 文档|*.pdf", FileName = Path.GetFileName(_pdf.Path) };
        if (dialog.ShowDialog(this) != true) return;
        try { await _pdf.ExportAsync(dialog.FileName); StatusText.Text = "PDF 已导出"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Notify(ex.Message); }
    }
    private void Diagnostic_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DiagnosticsList.SelectedItem is not Diagnostic d) return;
        if (d.FilePath is not null && File.Exists(d.FilePath)) OpenPath(d.FilePath);
        if (d.Line > 0) JumpToLine(d.Line);
    }
    private void AutoCompile_Changed(object sender, RoutedEventArgs e) { if (!_initialized) return; _settings.AutoCompile = AutoCompile.IsChecked == true; SettingsStore.Save(_settings); }

    private void ToggleAi_Click(object sender, RoutedEventArgs e) { AiPanel.Visibility = AiPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; UpdateAiScope(); }
    private void UpdateAiScope()
    {
        if (!_initialized || _active is null) return;
        AiScope.Text = Editor.SelectionLength > 0 ? $"将发送选中的 {Editor.SelectionLength:N0} 个字符和编译错误。" : $"将发送当前文件的 {Editor.Text.Length:N0} 个字符和编译错误；其他文件不会自动发送。";
    }
    private void AiFix_Click(object sender, RoutedEventArgs e) { AiPanel.Visibility = Visibility.Visible; AiPrompt.Text = "修复当前 LaTeX 编译错误，尽量少改动原文。"; UpdateAiScope(); }
    private void AiPolish_Click(object sender, RoutedEventArgs e) { AiPanel.Visibility = Visibility.Visible; AiPrompt.Text = "检查选中段落的拼写、语法和学术表达，保留数学公式、命令和引用，保持原意。"; UpdateAiScope(); }
    private void AiExplain_Click(object sender, RoutedEventArgs e) { AiPanel.Visibility = Visibility.Visible; AiPrompt.Text = "用中文解释当前编译错误、可能原因和定位方法，仅解释，不修改源码，changes 返回空数组。"; UpdateAiScope(); }
    private async void AiSend_Click(object sender, RoutedEventArgs e)
    {
        if (_active is null || _aiCancel is not null) return;
        if (string.IsNullOrWhiteSpace(_settings.AiModel)) { Settings_Click(sender, e); if (string.IsNullOrWhiteSpace(_settings.AiModel)) return; }
        if (string.IsNullOrWhiteSpace(AiPrompt.Text)) { AiStatus.Text = "请先描述需要 AI 帮你处理的问题。"; return; }
        var doc = _active; var offset = Editor.SelectionLength > 0 ? Editor.SelectionStart : 0;
        var text = Editor.SelectionLength > 0 ? Editor.SelectedText : doc.Document.Text;
        _aiCancel = new(); AiSendButton.IsEnabled = false; AiCancelButton.Visibility = Visibility.Visible; AiApplyButton.Visibility = Visibility.Collapsed;
        AiStatus.Text = "正在请求 AI…"; AiExplanation.Text = ""; _proposal = null;
        AiBefore.Visibility = AiAfter.Visibility = AiBeforeLabel.Visibility = AiAfterLabel.Visibility = Visibility.Collapsed;
        try
        {
            var diagnostics = string.Join("\n", (DiagnosticsList.ItemsSource as IReadOnlyList<Diagnostic> ?? []).Select(d => $"{Path.GetFileName(d.FilePath)}:{d.Line}: {d.Message}"));
            var proposal = await new AiService().SuggestAsync(_settings, CredentialStore.Read(_settings.AiEndpoint), text, AiPrompt.Text, diagnostics, _aiCancel.Token);
            var result = AiService.ValidateAndApply(text, proposal); AiExplanation.Text = proposal.Explanation;
            AiStatus.Text = proposal.Changes.Count == 0 ? "AI 已完成解释，源码没有改动。" : $"有 {proposal.Changes.Count} 处建议，请核对后应用。";
            if (proposal.Changes.Count > 0)
            {
                _proposal = (doc, text, offset, result);
                AiBefore.Text = string.Join("\n\n", proposal.Changes.Select(c => c.Old)); AiAfter.Text = string.Join("\n\n", proposal.Changes.Select(c => c.New));
                AiBefore.Visibility = AiAfter.Visibility = AiBeforeLabel.Visibility = AiAfterLabel.Visibility = AiApplyButton.Visibility = Visibility.Visible;
            }
        }
        catch (OperationCanceledException) when (_aiCancel.IsCancellationRequested) { AiStatus.Text = "请求已取消，源码没有改动。"; }
        catch (OperationCanceledException) { AiStatus.Text = "AI 服务响应超时，源码没有改动。请缩小选区或稍后重试。"; }
        catch (JsonException) { AiStatus.Text = "AI 返回的修改格式无法解析，源码没有改动。请重新发送或换一个模型。"; }
        catch (Exception ex) { AiStatus.Text = ex.Message; }
        finally { _aiCancel.Dispose(); _aiCancel = null; AiSendButton.IsEnabled = true; AiCancelButton.Visibility = Visibility.Collapsed; }
    }
    private void AiCancel_Click(object sender, RoutedEventArgs e) => _aiCancel?.Cancel();
    private async void AiApply_Click(object sender, RoutedEventArgs e)
    {
        if (_proposal is not { } p) return;
        if (!_documents.Contains(p.Doc) || p.Offset + p.Source.Length > p.Doc.Document.TextLength || p.Doc.Document.GetText(p.Offset, p.Source.Length) != p.Source)
        { AiStatus.Text = "这段源码已经变化，请重新生成建议。"; _proposal = null; AiApplyButton.Visibility = Visibility.Collapsed; return; }
        SwitchTo(p.Doc);
        using (p.Doc.Document.RunUpdate()) p.Doc.Document.Replace(p.Offset, p.Source.Length, p.Result);
        AiApplyButton.Visibility = Visibility.Collapsed; _proposal = null; AiStatus.Text = "修改已应用，可按 Ctrl+Z 撤销。正在验证编译…";
        await CompileAsync(); AiStatus.Text = "修改已应用，可按 Ctrl+Z 撤销。" + StatusText.Text;
    }
    private void Settings_Click(object sender, RoutedEventArgs e) { var dialog = new SettingsWindow(_settings) { Owner = this }; if (dialog.ShowDialog() == true) ApplySettings(); }

    private void Find_Click(object sender, RoutedEventArgs e) { FindBar.Visibility = Visibility.Visible; if (Editor.SelectionLength > 0) FindText.Text = Editor.SelectedText; FindText.Focus(); FindText.SelectAll(); }
    private void CloseFind_Click(object sender, RoutedEventArgs e) { FindBar.Visibility = Visibility.Collapsed; Editor.Focus(); }
    private void FindText_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { FindNext_Click(sender, e); e.Handled = true; } if (e.Key == Key.Escape) CloseFind_Click(sender, e); }
    private bool FindNext()
    {
        if (string.IsNullOrEmpty(FindText.Text)) return false;
        var at = Editor.Text.IndexOf(FindText.Text, Math.Min(Editor.SelectionStart + Editor.SelectionLength, Editor.Text.Length), StringComparison.OrdinalIgnoreCase);
        if (at < 0) at = Editor.Text.IndexOf(FindText.Text, StringComparison.OrdinalIgnoreCase);
        if (at < 0) { StatusText.Text = "没有找到匹配文本"; return false; }
        Editor.Select(at, FindText.Text.Length); Editor.ScrollToLine(Editor.Document.GetLineByOffset(at).LineNumber); return true;
    }
    private void FindNext_Click(object sender, RoutedEventArgs e) => FindNext();
    private void Replace_Click(object sender, RoutedEventArgs e) { if (Editor.SelectedText.Equals(FindText.Text, StringComparison.OrdinalIgnoreCase) && FindText.Text.Length > 0) Editor.Document.Replace(Editor.SelectionStart, Editor.SelectionLength, ReplaceText.Text); FindNext(); }
    private void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(FindText.Text)) return;
        var text = Editor.Text; var result = text.Replace(FindText.Text, ReplaceText.Text, StringComparison.OrdinalIgnoreCase);
        using (Editor.Document.RunUpdate()) Editor.Document.Replace(0, Editor.Document.TextLength, result);
        StatusText.Text = "已替换全部匹配文本 · Ctrl+Z 撤销";
    }
    private void Comment_Click(object sender, RoutedEventArgs e)
    {
        var first = Editor.Document.GetLineByOffset(Editor.SelectionStart).LineNumber;
        var lastOffset = Editor.SelectionStart + Math.Max(0, Editor.SelectionLength - 1);
        var last = Editor.Document.GetLineByOffset(lastOffset).LineNumber;
        var uncomment = Enumerable.Range(first, last - first + 1).All(i => Editor.Document.GetText(Editor.Document.GetLineByNumber(i)).TrimStart().StartsWith('%'));
        using (Editor.Document.RunUpdate())
        {
            for (var i = last; i >= first; i--)
            {
                var line = Editor.Document.GetLineByNumber(i); var text = Editor.Document.GetText(line);
                if (uncomment) { var position = text.IndexOf('%'); var count = position + 1 < text.Length && text[position + 1] == ' ' ? 2 : 1; Editor.Document.Remove(line.Offset + position, count); }
                else Editor.Document.Insert(line.Offset, "% ");
            }
        }
    }

    private void Editor_TextEntered(object? sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        var currentLine = Editor.Document.GetLineByOffset(Editor.CaretOffset);
        var prefix = Editor.Document.GetText(currentLine.Offset, Editor.CaretOffset - currentLine.Offset);
        if (LatexParser.RemoveComment(prefix).Length < prefix.Length) return;
        if (e.Text == "\\")
        {
            var commands = new[] { "section", "subsection", "subsubsection", "chapter", "textbf", "textit", "emph", "label", "ref", "eqref", "cite", "includegraphics", "input", "include", "usepackage", "documentclass", "frac", "sqrt", "sum", "int", "alpha", "beta", "gamma", "theta", "lambda", "pi", "infty", "times", "cdot", "left", "right" };
            var items = commands.Select(c => new CompletionItem(c, CommandSnippet(c), "LaTeX 命令")).ToList();
            foreach (var env in new[] { "equation", "align", "itemize", "enumerate", "figure", "table", "abstract" }) items.Add(new("begin{" + env + "}", "begin{" + env + "}\n  ${cursor}\n\\end{" + env + "}", "插入完整环境"));
            ShowCompletions(items);
        }
        else if (e.Text == "{")
        {
            var offset = Editor.CaretOffset; var before = Editor.Document.GetText(0, offset);
            var backslashes = 0;
            for (var i = offset - 2; i >= 0 && Editor.Document.GetCharAt(i) == '\\'; i--) backslashes++;
            if (backslashes % 2 == 1) return;
            if (offset >= Editor.Document.TextLength || Editor.Document.GetCharAt(offset) != '}') { Editor.Document.Insert(offset, "}"); Editor.CaretOffset = offset; }
            if (Regex.IsMatch(before, @"\\(?:eqref|ref|pageref|autoref)\{$")) ShowCompletions(LatexParser.GetReferenceKeys(Editor.Text).Select(c => new CompletionItem(c)));
            else if (Regex.IsMatch(before, @"\\(?:cite|citep|citet|parencite|textcite)\{$"))
            {
                var keys = new List<string>();
                if (_projectRoot is not null) foreach (var bib in Directory.GetFiles(_projectRoot, "*.bib")) { try { keys.AddRange(LatexParser.GetCitationKeys(File.ReadAllText(bib))); } catch (IOException) { } }
                ShowCompletions(keys.Distinct().Select(c => new CompletionItem(c)));
            }
        }
    }
    internal static string CommandSnippet(string command) => command switch
    {
        "frac" => "frac{${cursor}}{}",
        "left" => "left(${cursor}\\right)",
        "right" => "right)",
        "sum" or "int" => command + "_{${cursor}}^{}",
        "alpha" or "beta" or "gamma" or "theta" or "lambda" or "pi" or "infty" or "times" or "cdot" => command + "${cursor}",
        _ => command + "{${cursor}}"
    };
    private void ShowCompletions(IEnumerable<CompletionItem> items)
    {
        var list = items.ToList(); if (list.Count == 0) return;
        _completion?.Close(); _completion = new CompletionWindow(Editor.TextArea);
        foreach (var item in list) _completion.CompletionList.CompletionData.Add(item);
        _completion.Closed += (_, _) => _completion = null; _completion.Show();
    }
    private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_completion is not null && (e.Key == Key.Tab || e.Key == Key.Enter)) { _completion.CompletionList.RequestInsertion(e); e.Handled = true; }
    }
    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0; var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        if (ctrl)
        {
            switch (e.Key)
            {
                case Key.Enter: if (_building) _buildCancel?.Cancel(); else await CompileAsync(); break;
                case Key.S: if (_active is not null) SaveDocument(_active, shift); break;
                case Key.O: Open_Click(sender, e); break;
                case Key.N: New_Click(sender, e); break;
                case Key.F: case Key.H: Find_Click(sender, e); if (e.Key == Key.H) ReplaceText.Focus(); break;
                case Key.J: ForwardSearch_Click(sender, e); break;
                case Key.OemQuestion: Comment_Click(sender, e); break;
                default: return;
            }
            e.Handled = true;
        }
        else if (e.Key == Key.F11) { Focus_Click(sender, e); e.Handled = true; }
        else if (e.Key == Key.Escape) { if (_completion is not null) _completion.Close(); else if (AiPanel.Visibility == Visibility.Visible) AiPanel.Visibility = Visibility.Collapsed; else FindBar.Visibility = Visibility.Collapsed; }
    }
    private void ToggleSidebar_Click(object sender, RoutedEventArgs e) { SidebarColumn.Width = SidebarColumn.Width.Value == 0 ? new GridLength(210) : new GridLength(0); }
    private void Focus_Click(object sender, RoutedEventArgs e) { _focus = !_focus; SidebarColumn.Width = new GridLength(_focus ? 0 : 210); PreviewPane.Visibility = _focus ? Visibility.Collapsed : Visibility.Visible; PreviewColumn.MinWidth = _focus ? 0 : 270; PreviewColumn.Width = _focus ? new GridLength(0) : new GridLength(1, GridUnitType.Star); AiPanel.Visibility = Visibility.Collapsed; }
    private void Theme_Click(object sender, RoutedEventArgs e) { _settings.DarkMode = !_settings.DarkMode; ApplySettings(); SettingsStore.Save(_settings); }
    private void ToggleDiagnostics_Click(object sender, RoutedEventArgs e) => DiagnosticsPanel.Visibility = DiagnosticsPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    private void CloseDiagnostics_Click(object sender, RoutedEventArgs e) => DiagnosticsPanel.Visibility = Visibility.Collapsed;
    private void Shortcuts_Click(object sender, RoutedEventArgs e) => MessageBox.Show(this, "Ctrl+N  新建\nCtrl+O  打开\nCtrl+S  保存\nCtrl+Shift+S  另存为\nCtrl+Enter  保存并编译 / 取消编译\nCtrl+F / Ctrl+H  查找 / 替换\nCtrl+/  注释 / 取消注释\nCtrl+J  定位 PDF\nCtrl+点击 PDF  定位源码\nCtrl+滚轮  缩放 PDF\nF11  专注写作\nCtrl+Z  撤销（包括 AI 修改）", "EasyLatex 快捷键");
    private void About_Click(object sender, RoutedEventArgs e) => MessageBox.Show(this, "EasyLatex 0.1.0\n一个轻量 LaTeX 一键使用编译器。\n\nWindows 原生界面 · 本地写作 · 可选 API AI 助手\n基于 AvalonEdit，使用 Windows 原生 PDF 渲染。\nMIT 开源。", "关于 EasyLatex");
    private void Window_Drop(object sender, DragEventArgs e) { if (e.Data.GetData(DataFormats.FileDrop) is string[] files) foreach (var file in files.Where(File.Exists)) OpenPath(file); }
    private void Notify(string message) { StatusText.Text = message; if (!_verificationMode) MessageBox.Show(this, message, "EasyLatex", MessageBoxButton.OK, MessageBoxImage.Warning); }
    private void WriteRecovery()
    {
        try
        {
            var unsaved = _documents.Where(d => d.IsDirty || d.FilePath is null && d.Document.Text != Templates.Article && d.Document.Text.Length > 0).Select(d => new RecoveryFile(d.FilePath, d.Document.Text)).ToList();
            Directory.CreateDirectory(SettingsStore.DirectoryPath);
            var path = Path.Combine(SettingsStore.DirectoryPath, "recovery.json");
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(unsaved)); File.Move(path + ".tmp", path, true);
        }
        catch (IOException) { }
    }
    private void RestoreRecovery(bool acceptForVerification = false)
    {
        if (_verificationMode && !acceptForVerification) return;
        var path = Path.Combine(SettingsStore.DirectoryPath, "recovery.json");
        try
        {
            if (!File.Exists(path)) return; var files = JsonSerializer.Deserialize<List<RecoveryFile>>(File.ReadAllText(path));
            if (files is null || files.Count == 0) return;
            if (!acceptForVerification && MessageBox.Show(this, $"发现 {files.Count} 份上次未保存的内容，是否恢复？", "恢复写作", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            foreach (var file in files) { var d = new DocumentSession(file.Text, file.Path); d.MarkDirty(); AddDocument(d); }
        }
        catch (Exception ex) when (ex is IOException or JsonException) { StatusText.Text = "恢复记录无法读取，请检查本机 EasyLatex 数据目录"; }
    }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_verificationMode) foreach (var doc in _documents) if (!ConfirmClose(doc)) { e.Cancel = true; return; }
        _buildCancel?.Cancel(); _aiCancel?.Cancel(); _pdfCancel?.Cancel(); _recoveryTimer.Stop();
        _settings.WindowWidth = ActualWidth; _settings.WindowHeight = ActualHeight; SettingsStore.Save(_settings);
        var recovery = Path.Combine(SettingsStore.DirectoryPath, "recovery.json"); if (File.Exists(recovery)) File.Delete(recovery);
    }
    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); if (child is T match) return match;
            if (FindDescendant<T>(child) is { } result) return result;
        }
        return null;
    }
}
