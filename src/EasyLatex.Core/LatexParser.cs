using System.Text.RegularExpressions;

namespace EasyLatex.Core;

public static class LatexParser
{
    private static readonly Regex Sections = new(@"^\s*\\(?<kind>part|chapter|section|subsection|subsubsection)\*?(?:\[[^\]]*\])?\{", RegexOptions.Compiled);
    private static readonly Regex FileError = new(@"^(?<file>.+\.(?:tex|sty|cls|bib)):(?<line>\d+):\s*(?<message>.*)$", RegexOptions.Compiled);
    private static readonly Regex LogLine = new(@"^l\.(?<line>\d+)\s*(?<code>.*)$", RegexOptions.Compiled);
    public static IReadOnlyList<OutlineEntry> GetOutline(string source)
    {
        var result = new List<OutlineEntry>();
        var lines = source.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var text = RemoveComment(lines[i]);
            var m = Sections.Match(text);
            if (!m.Success) continue;
            var depth = m.Groups["kind"].Value switch { "subsection" => 1, "subsubsection" => 2, _ => 0 };
            var start = m.Index + m.Length;
            var braces = 1;
            for (var at = start; at < text.Length; at++)
            {
                if (text[at] == '\\') { at++; continue; }
                if (text[at] == '{') braces++;
                if (text[at] == '}' && --braces == 0)
                { result.Add(new(text[start..at].Trim(), i + 1, depth)); break; }
            }
        }
        return result;
    }

    public static string RemoveComment(string line)
    {
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] != '%') continue;
            var slashes = 0;
            for (var j = i - 1; j >= 0 && line[j] == '\\'; j--) slashes++;
            if (slashes % 2 == 0) return line[..i];
        }
        return line;
    }

    public static string ResolveMaster(string filePath, string source, string? projectRoot = null)
    {
        var m = Regex.Match(source, @"(?im)^\s*%\s*!\s*TeX\s+root\s*=\s*(.+)$");
        if (m.Success) return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(filePath)!, m.Groups[1].Value.Trim()));
        if (Regex.IsMatch(string.Join('\n', source.Split('\n').Select(RemoveComment)), @"\\documentclass(?:\[|\{)")) return Path.GetFullPath(filePath);
        var root = projectRoot ?? Path.GetDirectoryName(filePath)!;
        var candidates = Directory.Exists(root) ? Directory.GetFiles(root, "*.tex").Where(p => !string.Equals(Path.GetFullPath(p), Path.GetFullPath(filePath), StringComparison.OrdinalIgnoreCase))
            .Select(p => (Path: p, Text: File.ReadAllText(p))).Where(p => Regex.IsMatch(p.Text, @"(?m)^\s*\\documentclass(?:\[|\{)")).ToList() : [];
        var references = candidates.Where(c => Regex.Matches(c.Text, @"\\(?:input|include)\s*\{([^}]+)\}").Any(r =>
        {
            var path = Path.Combine(Path.GetDirectoryName(c.Path)!, r.Groups[1].Value);
            if (!Path.HasExtension(path)) path += ".tex";
            return string.Equals(Path.GetFullPath(path), Path.GetFullPath(filePath), StringComparison.OrdinalIgnoreCase);
        })).ToList();
        if (references.Count == 1) return Path.GetFullPath(references[0].Path);
        if (candidates.Count == 1) return Path.GetFullPath(candidates[0].Path);
        if (candidates.Count > 1) throw new IOException("项目有多个主文件。请在当前子文件头部指定 % !TeX root = main.tex，再编译。");
        return Path.GetFullPath(filePath);
    }

    public static EngineKind ResolveEngine(string source, EngineKind preference)
    {
        if (preference != EngineKind.Auto) return preference;
        var m = Regex.Match(source, @"(?im)^\s*%\s*!\s*TeX\s+program\s*=\s*(\w+)");
        return m.Groups[1].Value.ToLowerInvariant() switch
        {
            "pdflatex" => EngineKind.PdfLaTeX,
            "lualatex" => EngineKind.LuaLaTeX,
            "tectonic" => EngineKind.Tectonic,
            "xelatex" => EngineKind.XeLaTeX,
            _ => Regex.IsMatch(source, @"\\(?:usepackage(?:\[[^\]]*\])?\{(?:fontspec|xeCJK)|documentclass(?:\[[^\]]*\])?\{ctex)")
                || source.Any(c => c is >= '\u4e00' and <= '\u9fff') ? EngineKind.XeLaTeX : EngineKind.PdfLaTeX
        };
    }

    public static IReadOnlyList<Diagnostic> ParseDiagnostics(string output, string mainFile)
    {
        var items = new List<Diagnostic>();
        string? pendingError = null;
        string? pendingWarning = null;
        (string Path, int Line, DiagnosticSeverity Severity)? pendingLocation = null;
        foreach (var raw in output.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (pendingLocation is { } location && line.Length > 0)
            {
                items.Add(new(location.Severity, line, location.Path, location.Line)); pendingLocation = null; continue;
            }
            var prefixedError = line.StartsWith("error:", StringComparison.OrdinalIgnoreCase);
            var prefixedWarning = line.StartsWith("warning:", StringComparison.OrdinalIgnoreCase);
            if (prefixedError) line = line[6..].Trim();
            if (prefixedWarning) line = line[8..].Trim();
            var m = FileError.Match(line);
            if (m.Success)
            {
                var path = m.Groups["file"].Value;
                if (!Path.IsPathRooted(path)) path = Path.Combine(Path.GetDirectoryName(mainFile)!, path);
                var message = m.Groups["message"].Value;
                var severity = prefixedWarning || message.Contains("Warning", StringComparison.OrdinalIgnoreCase) ? DiagnosticSeverity.Warning : DiagnosticSeverity.Error;
                if (message.Length == 0) pendingLocation = (Path.GetFullPath(path), int.Parse(m.Groups["line"].Value), severity);
                else items.Add(new(severity, message, Path.GetFullPath(path), int.Parse(m.Groups["line"].Value)));
                pendingError = null;
                continue;
            }
            if (prefixedError || prefixedWarning) { items.Add(new(prefixedError ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning, line, mainFile)); continue; }
            if (line.StartsWith("! ")) { pendingError = line[2..]; continue; }
            var lm = LogLine.Match(line);
            if (lm.Success && pendingError is not null)
            {
                items.Add(new(DiagnosticSeverity.Error, pendingError, mainFile, int.Parse(lm.Groups["line"].Value)));
                pendingError = null;
                continue;
            }
            if (line.Contains("LaTeX Warning:") || line.Contains("Package ") && line.Contains(" Warning:"))
            {
                pendingWarning = line;
                var n = Regex.Match(line, @"input line (\d+)");
                if (n.Success || line.EndsWith('.'))
                {
                    items.Add(new(DiagnosticSeverity.Warning, line, mainFile, n.Success ? int.Parse(n.Groups[1].Value) : 0));
                    pendingWarning = null;
                }
                continue;
            }
            if (pendingWarning is not null)
            {
                pendingWarning += " " + line;
                if (line.EndsWith('.')) { items.Add(new(DiagnosticSeverity.Warning, pendingWarning, mainFile)); pendingWarning = null; }
            }
            if (Regex.IsMatch(line, @"^(?:Overfull|Underfull) \\[hv]box"))
                items.Add(new(DiagnosticSeverity.Information, line, mainFile));
            if (line.StartsWith("error:", StringComparison.OrdinalIgnoreCase) && !FileError.IsMatch(line[6..].Trim()))
                items.Add(new(DiagnosticSeverity.Error, line[6..].Trim(), mainFile));
        }
        if (pendingError is not null) items.Add(new(DiagnosticSeverity.Error, pendingError, mainFile));
        return items.Distinct().Take(200).ToList();
    }

    public static IReadOnlyList<string> GetReferenceKeys(string source) => Regex.Matches(source, @"\\label\{([^}]+)\}").Select(m => m.Groups[1].Value).Distinct().ToList();
    public static IReadOnlyList<string> GetCitationKeys(string source) => Regex.Matches(source, @"@\w+\s*\{\s*([^,\s]+)", RegexOptions.IgnoreCase).Select(m => m.Groups[1].Value).Distinct().ToList();
}
