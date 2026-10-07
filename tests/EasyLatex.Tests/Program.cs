using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EasyLatex;
using EasyLatex.Core;
using EasyLatex.Services;
using ICSharpCode.AvalonEdit;

internal static class Program
{
    private static int _passed, _failed;
    private static string Root = "";
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--child-sleep")) { Thread.Sleep(60_000); return 0; }
        Root = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--")) ?? "artifacts/verification");
        Directory.CreateDirectory(Root); Environment.SetEnvironmentVariable("EASYLATEX_DATA_DIR", Path.Combine(Root, "user-data"));
        Environment.SetEnvironmentVariable("TECTONIC_CACHE_DIR", Path.Combine(Root, "tectonic-cache"));
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var app = new App(); app.InitializeComponent();
        app.Dispatcher.InvokeAsync(async () =>
        {
            try { if (!args.Contains("--ui-only")) await CoreChecks(); if (args.Contains("--ui") || args.Contains("--ui-only")) await UiChecks(); }
            catch (Exception ex) { Fail("harness", ex.ToString()); }
            finally
            {
                File.WriteAllText(Path.Combine(Root, "results.json"), JsonSerializer.Serialize(new { passed = _passed, failed = _failed, timestamp = DateTimeOffset.UtcNow }));
                Console.WriteLine($"RESULT {_passed} passed, {_failed} failed"); app.Shutdown(_failed == 0 ? 0 : 1); Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
            }
        });
        Dispatcher.Run(); return _failed == 0 ? 0 : 1;
    }
    private static void Check(string name, bool condition, string detail = "")
    {
        if (condition) { _passed++; Console.WriteLine("PASS " + name); }
        else Fail(name, detail);
    }
    private static void Fail(string name, string detail) { _failed++; Console.WriteLine("FAIL " + name + " " + detail); }
    private static void Reject(string name, Action action)
    {
        try { action(); Fail(name, "unexpected acceptance"); }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException) { Check(name, true); }
    }
    private sealed class MockHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => responder(request);
    }
    private static async Task CoreChecks()
    {
        Check("outline-comments-and-depth", LatexParser.GetOutline("% \\section{hidden}\n\\section{A}\n\\subsection{B}\n\\subsubsection*{C}").SequenceEqual(new[] { new OutlineEntry("A", 2, 0), new OutlineEntry("B", 3, 1), new OutlineEntry("C", 4, 2) }));
        Check("escaped-percent", LatexParser.RemoveComment("50\\% yes % hidden") == "50\\% yes ");
        Check("master-relative", LatexParser.ResolveMaster(Path.Combine(Root, "chapter", "part.tex"), "% !TEX root = ../main.tex") == Path.Combine(Root, "main.tex"));
        Check("engine-chinese", LatexParser.ResolveEngine("中文", EngineKind.Auto) == EngineKind.XeLaTeX);
        Check("engine-magic", LatexParser.ResolveEngine("% !TeX program = lualatex\n中文", EngineKind.Auto) == EngineKind.LuaLaTeX);
        Check("engine-preference", LatexParser.ResolveEngine("% !TeX program = lualatex", EngineKind.PdfLaTeX) == EngineKind.PdfLaTeX);
        var diagnostics = LatexParser.ParseDiagnostics("C:/paper/main.tex:12: Undefined control sequence.\n! Missing $ inserted.\nl.8 a_b\nLaTeX Warning: Reference `x' undefined on input line 20.", Path.Combine(Root, "main.tex"));
        Check("diagnostics-line-and-severity", diagnostics.Count == 3 && diagnostics[0].Line == 12 && diagnostics[1].Line == 8 && diagnostics[2].Severity == DiagnosticSeverity.Warning, JsonSerializer.Serialize(diagnostics));
        var p = new AiProposal("", [new("abc", "XYZ"), new("def", "D")]);
        Check("ai-atomic-patch", AiService.ValidateAndApply("abc def", p) == "XYZ D");
        Reject("ai-reject-duplicate", () => AiService.ValidateAndApply("abc abc", new("", [new("abc", "x")])));
        Reject("ai-reject-overlap", () => AiService.ValidateAndApply("abcdef", new("", [new("abc", "x"), new("bcde", "y")])));
        Reject("ai-reject-missing", () => AiService.ValidateAndApply("abc", new("", [new("no", "yes")])));
        Reject("ai-reject-http-remote", () => AiService.GetEndpoint("http://example.com/v1"));
        Reject("ai-reject-credentials-in-url", () => AiService.GetEndpoint("https://user:password@example.com/v1"));
        Check("ai-local-endpoint", AiService.GetEndpoint("http://localhost:11434/v1").AbsoluteUri == "http://localhost:11434/v1/chat/completions");
        var handler = new MockHandler(async request =>
        {
            Check("ai-request-auth", request.Headers.Authorization?.Parameter == "test-key");
            var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Check("ai-request-protocol", payload.RootElement.GetProperty("model").GetString() == "test-model" && payload.RootElement.GetProperty("messages").GetArrayLength() == 2);
            var content = JsonSerializer.Serialize(new { explanation = "修复缺失括号", changes = new[] { new { old = "\\textbf{test", @new = "\\textbf{test}" } } });
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content } } } })) };
        });
        var suggestion = await new AiService(new HttpClient(handler)).SuggestAsync(new() { AiModel = "test-model" }, "test-key", "\\textbf{test", "修复", "Missing }", default);
        Check("ai-response-parsing", AiService.ValidateAndApply("\\textbf{test", suggestion) == "\\textbf{test}");

        using (var cancel = new CancellationTokenSource(500))
        {
            var start = Stopwatch.StartNew();
            try { await ProcessRunner.RunAsync(Environment.ProcessPath!, ["--child-sleep"], Root, token: cancel.Token); Fail("process-cancel", "did not cancel"); }
            catch (OperationCanceledException) { Check("process-cancel-kills", start.Elapsed < TimeSpan.FromSeconds(4)); }
        }

        var directory = Path.Combine(Root, "中文 project with spaces"); Directory.CreateDirectory(directory);
        var english = Path.Combine(directory, "main.tex"); await File.WriteAllTextAsync(english, Templates.Article);
        var settings = new AppSettings { Engine = EngineKind.XeLaTeX };
        var compiler = new CompilerService();
        var result = await compiler.BuildAsync(english, settings, null, default);
        await File.WriteAllTextAsync(Path.Combine(Root, "english-build.log"), result.Output);
        Check("compile-english-unicode-spaces", result.Success && result.PdfPath is not null && new FileInfo(result.PdfPath).Length > 5000, result.Output[^Math.Min(result.Output.Length, 1000)..]);
        if (result.Success)
        {
            var sync = new SyncTexService(); sync.Load(result.PdfPath!);
            Check("synctex-map-present", sync.Points.Count > 0);
            var forward = sync.Forward(english, 22);
            Check("synctex-forward", forward is not null && forward.Page == 1 && Math.Abs(forward.Line - 22) < 4, JsonSerializer.Serialize(forward));
            if (forward is not null) Check("synctex-roundtrip", sync.Reverse(forward.Page, forward.X, forward.Y) is { } reverse && Math.Abs(reverse.Line - forward.Line) < 4, JsonSerializer.Serialize(sync.Reverse(forward.Page, forward.X, forward.Y)));
        }
        var chinese = Path.Combine(directory, "中文.tex"); await File.WriteAllTextAsync(chinese, Templates.Chinese);
        var c = await compiler.BuildAsync(chinese, settings, null, default); await File.WriteAllTextAsync(Path.Combine(Root, "chinese-build.log"), c.Output);
        Check("compile-chinese-template", c.Success, c.Output[^Math.Min(c.Output.Length, 1000)..]);
        await File.WriteAllTextAsync(Path.Combine(directory, "chapter.tex"), "\\section{A chapter}\nSource files can be split. Reference \\cite{knuth1984}.\n");
        await File.WriteAllTextAsync(Path.Combine(directory, "refs.bib"), "@book{knuth1984, author={Donald E. Knuth}, title={The TeXbook}, year={1984}, publisher={Addison-Wesley}}");
        var multi = Path.Combine(directory, "multi.tex");
        await File.WriteAllTextAsync(multi, "\\documentclass{article}\n\\begin{document}\n\\input{chapter}\n\\bibliographystyle{plain}\n\\bibliography{refs}\n\\end{document}");
        var m = await compiler.BuildAsync(multi, settings, null, default); await File.WriteAllTextAsync(Path.Combine(Root, "multi-build.log"), m.Output);
        Check("compile-multi-file-bibliography", m.Success && File.Exists(Path.Combine(directory, ".easylatex", "build", "multi.bbl")), m.Output[^Math.Min(m.Output.Length, 1000)..]);
        await File.WriteAllTextAsync(english, Templates.Article.Replace("Every good paper", "\\unknowncommand Every good paper"));
        var broken = await compiler.BuildAsync(english, settings, null, default); await File.WriteAllTextAsync(Path.Combine(Root, "broken-build.log"), broken.Output);
        Check("compile-errors-not-stale-success", !broken.Success && broken.PdfPath is null && broken.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error && d.Line > 0));
        await File.WriteAllTextAsync(english, Templates.Article);
        if (CompilerService.FindTool("tectonic", Path.GetFullPath(".tools/tectonic")) is { } tectonic)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
            var t = await compiler.BuildAsync(english, new() { Engine = EngineKind.Tectonic, CompilerDirectory = Path.GetDirectoryName(tectonic)! }, line => Console.WriteLine("TECTONIC " + line), timeout.Token);
            await File.WriteAllTextAsync(Path.Combine(Root, "tectonic-build.log"), t.Output);
            Check("compile-bundled-tectonic", t.Success, t.Output[^Math.Min(t.Output.Length, 1000)..]);
        }
        else Fail("compile-bundled-tectonic", "engine not found");
    }

    private static async Task UiChecks()
    {
        var window = new MainWindow { WindowStartupLocation = WindowStartupLocation.Manual, Left = -5000, Top = -5000, ShowInTaskbar = false };
        window.Show(); await Task.Delay(200);
        var file = Path.Combine(Root, "中文 project with spaces", "main.tex");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        if (!File.Exists(file)) await File.WriteAllTextAsync(file, Templates.Article);
        window.OpenPath(file);
        var editor = (TextEditor)window.FindName("Editor");
        Check("ui-open-source", editor.Text.Contains("A small beginning"));
        editor.Document.Insert(editor.Text.Length, "\n% UI verification\n");
        await window.CompileAsync();
        Check("ui-save-build-preview", window.Pages.Count > 0 && window.Pages[0].Image is not null && File.ReadAllText(file).Contains("UI verification"));
        await Task.Delay(500);
        Capture(window, "main-light.png");
        window.Width = 1024; window.Height = 700; await Task.Delay(300); Capture(window, "main-compact.png");
        var ai = (Border)window.FindName("AiPanel"); ai.Visibility = Visibility.Visible; await Task.Delay(200); Capture(window, "ai-panel.png"); ai.Visibility = Visibility.Collapsed;
        var settings = new SettingsWindow(new()) { Owner = window, WindowStartupLocation = WindowStartupLocation.Manual, Left = -5000, Top = -5000, ShowInTaskbar = false };
        settings.Show(); await Task.Delay(200); Capture(settings, "settings.png"); settings.Close();
        editor.Document.Insert(editor.Text.IndexOf("Every good"), "\\unknowncommand ");
        await window.CompileAsync();
        Check("ui-failed-build-keeps-preview", window.Pages.Count > 0 && ((Border)window.FindName("DiagnosticsPanel")).Visibility == Visibility.Visible);
        Capture(window, "compile-error.png");
        editor.Document.UndoStack.Undo(); await window.CompileAsync();
        Check("ui-undo-recompile", ((TextBlock)window.FindName("StatusText")).Text.StartsWith("编译完成"));
        Console.WriteLine("PDF_NATIVE_SIZE " + window.Pages[0].PointWidth + "x" + window.Pages[0].PointHeight);
        window.Close();
    }
    private static void Capture(Window window, string name)
    {
        var element = (FrameworkElement)window.Content; element.UpdateLayout();
        var image = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var output = File.Create(Path.Combine(Root, name)); encoder.Save(output);
        Check("render-" + name, image.PixelWidth > 400 && output.Length > 10000);
    }
}
