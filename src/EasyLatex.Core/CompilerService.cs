using System.Diagnostics;
using System.Text;

namespace EasyLatex.Core;

public sealed class CompilerService
{
    public static string? FindTool(string name, string customDirectory = "")
    {
        var dirs = new List<string> { customDirectory, Path.Combine(AppContext.BaseDirectory, "tools"), Path.Combine(AppContext.BaseDirectory, "tools", "tectonic") };
        dirs.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator));
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        dirs.AddRange([Path.Combine(local, "Programs", "MiKTeX", "miktex", "bin", "x64"), @"C:\Program Files\MiKTeX\miktex\bin\x64"]);
        foreach (var root in new[] { @"C:\texlive", @"D:\texlive", @"D:\tex_live\texlive" })
        {
            if (Directory.Exists(root)) dirs.AddRange(Directory.GetDirectories(root).OrderDescending().Select(p => Path.Combine(p, "bin", "windows")));
        }
        foreach (var dir in dirs.Where(d => !string.IsNullOrWhiteSpace(d)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try { var file = Path.Combine(dir.Trim('"'), name.EndsWith(".exe") ? name : name + ".exe"); if (File.Exists(file)) return file; }
            catch (ArgumentException) { }
        }
        return null;
    }

    public static IReadOnlyList<CompilerTool> Detect(string customDirectory = "")
    {
        var tools = new List<CompilerTool>();
        foreach (var (kind, executable, label) in new[] { (EngineKind.XeLaTeX, "xelatex", "XeLaTeX"), (EngineKind.PdfLaTeX, "pdflatex", "pdfLaTeX"), (EngineKind.LuaLaTeX, "lualatex", "LuaLaTeX"), (EngineKind.Tectonic, "tectonic", "Tectonic") })
            if (FindTool(executable, customDirectory) is { } path) tools.Add(new(kind, path, label));
        return tools;
    }

    public async Task<BuildResult> BuildAsync(string masterFile, AppSettings settings, Action<string>? progress, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        var text = await File.ReadAllTextAsync(masterFile, token);
        var engine = LatexParser.ResolveEngine(text, settings.Engine);
        var tools = Detect(settings.CompilerDirectory);
        var tool = tools.FirstOrDefault(t => t.Kind == engine);
        if (tool is null && settings.Engine == EngineKind.Auto) tool = tools.FirstOrDefault(t => t.Kind == EngineKind.Tectonic);
        if (tool is null) return new(false, false, null, "", [new(DiagnosticSeverity.Error, "未找到编译引擎。请在设置中选择 TeX 目录，或安装轻量引擎。")], watch.Elapsed, engine.ToString());
        var directory = Path.GetDirectoryName(masterFile)!;
        var buildDir = Path.Combine(directory, ".easylatex", "build");
        Directory.CreateDirectory(buildDir);
        var stem = Path.GetFileNameWithoutExtension(masterFile);
        var pdf = Path.Combine(buildDir, stem + ".pdf");
        var started = DateTime.UtcNow;
        var output = new StringBuilder();
        try
        {
            int exit;
            if (tool.Kind == EngineKind.Tectonic)
            {
                var args = new List<string> { "-X", "compile", "--synctex", "--keep-logs", "--keep-intermediates", "--outdir", buildDir };
                if (!settings.AllowShellEscape) args.Add("--untrusted");
                if (settings.OfflineBuild) args.Add("--only-cached");
                args.Add(masterFile);
                var fontConfig = Path.Combine(buildDir, "fonts.conf");
                var fonts = System.Security.SecurityElement.Escape(Environment.GetFolderPath(Environment.SpecialFolder.Fonts).Replace('\\', '/'));
                var fontCache = System.Security.SecurityElement.Escape(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EasyLatex", "font-cache").Replace('\\', '/'));
                await File.WriteAllTextAsync(fontConfig, $"<?xml version=\"1.0\"?><fontconfig><dir>{fonts}</dir><cachedir>{fontCache}</cachedir></fontconfig>", token);
                var r = await ProcessRunner.RunAsync(tool.Path, args, directory, progress, token, new Dictionary<string, string> { ["FONTCONFIG_FILE"] = fontConfig });
                output.Append(r.Output); exit = r.ExitCode;
            }
            else if (FindTool("latexmk", settings.CompilerDirectory) is { } latexmk)
            {
                var flag = tool.Kind switch { EngineKind.XeLaTeX => "-pdfxe", EngineKind.LuaLaTeX => "-pdflua", _ => "-pdf" };
                var r = await ProcessRunner.RunAsync(latexmk,
                    ["-norc", flag, "-interaction=nonstopmode", "-file-line-error", "-synctex=1", settings.AllowShellEscape ? "-shell-escape" : "-latexoption=-no-shell-escape", "-outdir=" + buildDir, masterFile], directory, progress, token,
                    new Dictionary<string, string> { ["PATH"] = Path.GetDirectoryName(tool.Path) + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH") });
                output.Append(r.Output); exit = r.ExitCode;
            }
            else
            {
                exit = 0;
                for (var pass = 0; pass < 3; pass++)
                {
                    var r = await ProcessRunner.RunAsync(tool.Path,
                        ["-interaction=nonstopmode", "-halt-on-error", "-file-line-error", "-synctex=1", settings.AllowShellEscape ? "-shell-escape" : "-no-shell-escape", "-output-directory=" + buildDir, masterFile], directory, progress, token);
                    output.Append(r.Output); exit = r.ExitCode;
                    if (exit != 0) break;
                    if (pass == 0)
                    {
                        var bcf = Path.Combine(buildDir, stem + ".bcf");
                        var aux = Path.Combine(buildDir, stem + ".aux");
                        var bibName = File.Exists(bcf) ? "biber" : File.Exists(aux) && (await File.ReadAllTextAsync(aux, token)).Contains("\\bibdata") ? "bibtex" : null;
                        if (bibName is not null)
                        {
                            var bib = FindTool(bibName, settings.CompilerDirectory) ?? throw new IOException($"项目需要 {bibName}，但未找到该工具。请选择完整的 TeX 发行版。");
                            var b = await ProcessRunner.RunAsync(bib, [stem], buildDir, progress, token,
                                new Dictionary<string, string> { ["BIBINPUTS"] = directory + Path.PathSeparator, ["BSTINPUTS"] = directory + Path.PathSeparator });
                            output.Append(b.Output); exit = b.ExitCode;
                            if (exit != 0) break;
                        }
                    }
                    if (pass > 0 && !r.Output.Contains("Rerun") && !r.Output.Contains("undefined references")) break;
                }
            }
            var diagnostics = LatexParser.ParseDiagnostics(output.ToString(), masterFile).ToList();
            var success = exit == 0 && File.Exists(pdf) && File.GetLastWriteTimeUtc(pdf) >= started.AddSeconds(-2);
            // latexmk may correctly skip an up-to-date document.
            if (exit == 0 && File.Exists(pdf) && output.ToString().Contains("up-to-date")) success = true;
            if (!success && diagnostics.All(d => d.Severity != DiagnosticSeverity.Error)) diagnostics.Insert(0, new(DiagnosticSeverity.Error, $"编译失败（退出码 {exit}）。请展开日志查看详情。", masterFile));
            return new(success, false, success ? pdf : null, output.ToString(), diagnostics, watch.Elapsed, tool.Label);
        }
        catch (OperationCanceledException) { return new(false, true, null, output.ToString(), [], watch.Elapsed, tool.Label); }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        { return new(false, false, null, output.ToString(), [new(DiagnosticSeverity.Error, ex.Message, masterFile)], watch.Elapsed, tool.Label); }
    }
}
