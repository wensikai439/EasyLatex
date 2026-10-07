using System.Text.RegularExpressions;
using EasyLatex.Core;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;

namespace EasyLatex.EditorSupport;

public static class LatexFolding
{
    public static IEnumerable<NewFolding> Get(TextDocument document)
    {
        var stack = new Stack<(string Name, int Offset)>();
        var ranges = new List<NewFolding>();
        foreach (var line in document.Lines)
        {
            var text = LatexParser.RemoveComment(document.GetText(line));
            foreach (Match m in Regex.Matches(text, @"\\(begin|end)\{([^}]+)\}"))
            {
                if (m.Groups[1].Value == "begin") stack.Push((m.Groups[2].Value, line.Offset + m.Index));
                else if (stack.TryPeek(out var start) && start.Name == m.Groups[2].Value)
                {
                    stack.Pop();
                    if (document.GetLineByOffset(start.Offset).LineNumber != line.LineNumber)
                        ranges.Add(new(start.Offset, line.Offset + m.Index + m.Length) { Name = "\\begin{" + start.Name + "} …" });
                }
            }
        }
        return ranges.OrderBy(r => r.StartOffset);
    }
}
