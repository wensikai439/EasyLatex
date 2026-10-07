using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EasyLatex;
using EasyLatex.Core;
using EasyLatex.Models;
using EasyLatex.Services;
using ICSharpCode.AvalonEdit;

internal static partial class Program
{
    private static void SourceCheckpointChecks()
    {
        Check("sync-gesture-double-click-control-and-armed", MainWindow.IsReverseGesture(2, ModifierKeys.None, false) && MainWindow.IsReverseGesture(1, ModifierKeys.Control, false) && MainWindow.IsReverseGesture(1, ModifierKeys.None, true) && !MainWindow.IsReverseGesture(1, ModifierKeys.None, false));
        var source = new DocumentSession("first\r\nsecond\nthird\rfourth"); var map = new CompiledSource(source);
        var originalOffset = source.Document.GetLineByNumber(3).Offset;
        source.Document.Insert(0, "added\nmore\n");
        Check("sync-edited-source-forward-offset", map.ForwardLine(source, originalOffset + 11) == 3);
        Check("sync-edited-source-reverse-offset", map.ReverseLine(source, 3) == 5);
        source.Document.UndoStack.Undo(); Check("sync-offset-undo", map.ReverseLine(source, 3) == 3 && map.ForwardLine(source, originalOffset) == 3);
        Check("sync-reopened-identical-source", map.ReverseLine(new DocumentSession(source.Document.Text), 3) == 3);
        Check("sync-reopened-changed-source-rejected", map.ReverseLine(new DocumentSession("changed"), 3) is null);
    }

    private static async Task NavigationAndStressChecks()
    {
        SettingsStore.Save(new() { Engine = EngineKind.XeLaTeX });
        var folder = Path.Combine(Root, "navigation 中文"); Directory.CreateDirectory(folder);
        var main = Path.Combine(folder, "main.tex"); var child = Path.Combine(folder, "chapter.tex");
        await File.WriteAllTextAsync(main, "\\documentclass{article}\n\\begin{document}\nA beginning.\n\\input{chapter}\n\\end{document}");
        var content = new StringBuilder("% !TeX root = main.tex\n");
        for (var page = 1; page <= 40; page++)
        {
            content.AppendLine($"\\section{{Section {page}}}");
            content.AppendLine($"This is the distinctive paragraph on page {page}. It provides a navigation target.");
            content.AppendLine(@"\begin{equation} x^2 + y^2 = 1 \end{equation}");
            if (page < 40) content.AppendLine(@"\newpage");
        }
        await File.WriteAllTextAsync(child, content.ToString());
        var window = new MainWindow { WindowStartupLocation = WindowStartupLocation.Manual, Left = -5000, Top = -5000, ShowInTaskbar = false };
        window.EnableVerificationMode(); window.Show(); await Task.Delay(100); window.OpenPath(child);
        var editor = (TextEditor)window.FindName("Editor"); var forward = (Button)window.FindName("ForwardButton"); var reverse = (Button)window.FindName("ReverseButton");
        Check("ui-settings-visible-text", FindVisual<TextBlock>((Button)window.FindName("SettingsButton")).Any(t => t.Text == "设置"));
        Check("ui-bidirectional-buttons-visible", forward.IsVisible && reverse.IsVisible);
        await window.CompileAsync();
        Check("stress-real-40-page-compile", window.Pages.Count == 40 && ((TextBlock)window.FindName("StatusText")).Text.StartsWith("编译完成"));
        var pdf = Path.Combine(folder, ".easylatex", "build", "main.pdf"); var sync = new SyncTexService(); sync.Load(pdf);
        var targetLine = editor.Document.GetLineByOffset(editor.Text.IndexOf("paragraph on page 20", StringComparison.Ordinal)).LineNumber;
        editor.TextArea.Caret.Line = targetLine; forward.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var point = sync.Forward(child, targetLine)!;
        Check("ui-multifile-forward-correct-page", point.Page == 20 && window.Pages[19].HasMarker);
        var cli = CompilerService.FindTool("synctex");
        if (cli is not null)
        {
            var result = await ProcessRunner.RunAsync(cli, ["view", "-i", $"{targetLine}:0:{child}", "-o", pdf], folder);
            Check("sync-official-cli-page-agreement", Regex.IsMatch(result.Output, @"(?m)^Page:20\r?$") && point.Page == 20, result.Output);
            var inverse = await ProcessRunner.RunAsync(cli, ["edit", "-o", $"{point.Page}:{point.X.ToString(System.Globalization.CultureInfo.InvariantCulture)}:{point.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)}:{pdf}"], folder);
            Check("sync-official-cli-inverse-file-agreement", inverse.Output.Contains("chapter.tex") && sync.Reverse(point.Page, point.X, point.Y)?.FilePath == child, inverse.Output);
        }
        editor.Document.Insert(0, "% new line\n% another new line\n");
        editor.TextArea.Caret.Line = targetLine + 2; forward.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check("ui-forward-after-unsaved-lines", window.Pages[19].HasMarker && !File.ReadAllText(child).StartsWith("% new line"));
        reverse.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check("ui-reverse-one-shot-cursor", ((ListBox)window.FindName("PdfPages")).Cursor == Cursors.Cross);
        window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        Check("ui-reverse-escape-cancels", ((ListBox)window.FindName("PdfPages")).Cursor != Cursors.Cross && reverse.Content.ToString()!.Contains("定位源码"));
        reverse.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check("ui-reverse-after-unsaved-lines", window.NavigateFromPdf(point.Page, point.X, point.Y) && Math.Abs(editor.TextArea.Caret.Line - targetLine - 2) <= 1 && editor.SelectionLength > 0 && reverse.Content.ToString()!.Contains("定位源码"));
        Check("ui-reverse-margin-keeps-source", !window.NavigateFromPdf(20, -500, -500));
        window.OpenPath(main);
        Check("ui-reverse-opens-child-tab", window.NavigateFromPdf(point.Page, point.X, point.Y) && editor.Text.Contains("distinctive paragraph") && window.Title.StartsWith("chapter.tex"));
        var list = (ListBox)window.FindName("PdfPages"); var scroll = FindVisual<ScrollViewer>(list).First();
        var zoomIn = FindVisual<Button>(window).First(b => System.Windows.Automation.AutomationProperties.GetName(b) == "放大预览");
        var zoomOut = FindVisual<Button>(window).First(b => System.Windows.Automation.AutomationProperties.GetName(b) == "缩小预览");
        var fit = (Button)window.FindName("ZoomLabel"); var navigationTimes = new List<double>();
        foreach (var index in new[] { 0, 39, 8, 30, 17, 4, 38, 20, 10, 35, 1, 25 })
        {
            var watch = Stopwatch.StartNew(); list.ScrollIntoView(window.Pages[index]);
            await Until(() => window.Pages[index].Image is not null); navigationTimes.Add(watch.Elapsed.TotalMilliseconds);
            for (var repeat = 0; repeat < 4; repeat++) { zoomIn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); zoomOut.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
            fit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(() => window.Pages[index].Image is { } image && Math.Abs(image.PixelWidth - Math.Clamp(window.Pages[index].Width * System.Windows.Media.VisualTreeHelper.GetDpi(window).DpiScaleX, 200, 2400)) < 2);
        }
        await Task.Delay(100);
        Check("stress-pdf-cache-bounded", window.Pages.Count(p => p.Image is not null) <= 8);
        var panel = FindVisual<VirtualizingStackPanel>(list).First();
        Check("stress-pdf-virtualized", panel.Children.Count < 12 && panel.Children.Count < window.Pages.Count);
        list.ScrollIntoView(window.Pages[19]); await Until(() => window.Pages[19].Image is not null); await Task.Delay(100); var oldOffset = scroll.VerticalOffset;
        var diskTime = File.GetLastWriteTimeUtc(child); editor.Document.UndoStack.Undo(); await window.CompileAsync();
        Check("stress-recompile-retains-reading-position", Math.Abs(scroll.VerticalOffset - oldOffset) < window.Pages[0].Height / 2, $"{oldOffset} -> {scroll.VerticalOffset}");
        var savedTime = File.GetLastWriteTimeUtc(child); await window.CompileAsync();
        Check("stress-clean-compile-does-not-rewrite-source", File.GetLastWriteTimeUtc(child) == savedTime);
        var compressedMap = Path.ChangeExtension(pdf, ".synctex.gz"); var originalMap = await File.ReadAllBytesAsync(compressedMap);
        try
        {
            await File.WriteAllTextAsync(compressedMap, "broken compressed SyncTeX"); await window.LoadPdfAsync(pdf);
            Check("stress-invalid-synctex-keeps-pdf-preview", window.Pages.Count == 40 && window.Pages.Any(p => p.Image is not null) && !window.NavigateFromPdf(point.Page, point.X, point.Y));
        }
        finally { await File.WriteAllBytesAsync(compressedMap, originalMap); }
        for (var repeat = 0; repeat < 4; repeat++)
        {
            zoomIn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await window.LoadPdfAsync(pdf);
            fit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); list.ScrollIntoView(window.Pages[39]); await Until(() => window.Pages[39].Image is not null);
        }
        Check("stress-reload-during-render", window.Pages.Count == 40 && window.Pages[39].Image is not null && window.Pages.Count(p => p.Image is not null) <= 8);
        window.Width = 960; window.Height = 620; await Task.Delay(150);
        Check("ui-compact-navigation-controls-fit", reverse.ActualWidth > 60 && forward.ActualWidth > 60 && fit.ActualWidth > 40);
        Capture(window, "navigation-compact.png");
        Capture(window, "navigation-long-document.png");
        var process = Process.GetCurrentProcess(); process.Refresh(); navigationTimes.Sort();
        File.WriteAllText(Path.Combine(Root, "stress-performance.json"), JsonSerializer.Serialize(new { pages = window.Pages.Count, cachedPages = window.Pages.Count(p => p.Image is not null), realizedPages = panel.Children.Count, pageRenderP50Ms = navigationTimes[navigationTimes.Count / 2], pageRenderP95Ms = navigationTimes[^1], workingSetMiB = process.WorkingSet64 / 1048576.0, timestamp = DateTimeOffset.UtcNow }, new JsonSerializerOptions { WriteIndented = true }));
        window.Close();
    }
}
