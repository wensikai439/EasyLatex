using System.Text.RegularExpressions;

namespace EasyLatex.Core;

public static class LatexParser
{
    private static readonly Regex Sections = new(@"^\s*\\(?<kind>part|chapter|section|subsection|subsubsection)\*?(?:\[[^\]]*\])?\{(?<title>.*)\}", RegexOptions.Compiled);
    private static readonly Regex FileError = new(@"^(?<file>.+\.(?:tex|sty|cls|bib)):(?<line>\d+):\s*(?<message>.+)$", RegexOptions.Compiled);
    private static readonly Regex LogLine = new(@"^l\.(?<line>\d+)\s*(?<code>.*)$", RegexOptions.Compiled);
    public static IReadOnlyList<OutlineEntry> GetOutline(string source)
    {
        var result = new List<OutlineEntry>();
        var lines = source.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var m = Sections.Match(RemoveComment(lines[i]));
            if (!m.Success) continue;
            var depth = m.Groups["kind"].Value switch { "subsection" => 1, "subsubsection" => 2, _ => 0 };
            result.Add(new(m.Groups["title"].Value.Trim(), i + 1, depth));
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

    public static string ResolveMaster(string filePath, string source)
    {
        var m = Regex.Match(source, @"(?im)^\s*%\s*!\s*TeX\s+root\s*=\s*(.+)$");
        if (!m.Success) return Path.GetFullPath(filePath);
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(filePath)!, m.Groups[1].Value.Trim()));
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
        foreach (var raw in output.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            var m = FileError.Match(line);
            if (m.Success)
            {
                var path = m.Groups["file"].Value;
                if (!Path.IsPathRooted(path)) path = Path.Combine(Path.GetDirectoryName(mainFile)!, path);
                var message = m.Groups["message"].Value;
                items.Add(new(message.Contains("Warning", StringComparison.OrdinalIgnoreCase) ? DiagnosticSeverity.Warning : DiagnosticSeverity.Error,
                    message, Path.GetFullPath(path), int.Parse(m.Groups["line"].Value)));
                pendingError = null;
                continue;
            }
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
