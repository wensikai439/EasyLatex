namespace EasyLatex.Core;

public enum EngineKind { Auto, XeLaTeX, PdfLaTeX, LuaLaTeX, Tectonic }
public enum DiagnosticSeverity { Error, Warning, Information }
public sealed record Diagnostic(DiagnosticSeverity Severity, string Message, string? FilePath = null, int Line = 0)
{
    public string Location => Line > 0 ? $"{Path.GetFileName(FilePath)} · 第 {Line} 行" : "编译信息";
    public string Symbol => Severity == DiagnosticSeverity.Error ? "●" : "○";
}
public sealed record OutlineEntry(string Title, int Line, int Depth);
public sealed record CompilerTool(EngineKind Kind, string Path, string Label);
public sealed record BuildResult(bool Success, bool Cancelled, string? PdfPath, string Output,
    IReadOnlyList<Diagnostic> Diagnostics, TimeSpan Duration, string Engine);
public sealed record AiChange(string Old, string New);
public sealed record AiProposal(string Explanation, IReadOnlyList<AiChange> Changes);
public sealed record SyncPoint(string FilePath, int Line, int Page, double X, double Y, double Width, double Height);

public sealed class AppSettings
{
    public EngineKind Engine { get; set; } = EngineKind.Auto;
    public string CompilerDirectory { get; set; } = "";
    public bool AutoCompile { get; set; }
    public bool AllowShellEscape { get; set; }
    public bool OfflineBuild { get; set; }
    public bool DarkMode { get; set; }
    public double EditorFontSize { get; set; } = 15;
    public string AiEndpoint { get; set; } = "https://api.openai.com/v1";
    public string AiModel { get; set; } = "";
    public List<string> RecentFiles { get; set; } = [];
    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 840;
}
