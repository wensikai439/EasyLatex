using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace EasyLatex.EditorSupport;

public sealed class CompletionItem(string text, string? snippet = null, string description = "") : ICompletionData
{
    public ImageSource? Image => null;
    public string Text => text;
    public object Content => text;
    public object Description => description;
    public double Priority => 0;
    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        var code = snippet ?? text;
        var cursor = code.IndexOf("${cursor}", StringComparison.Ordinal);
        code = code.Replace("${cursor}", "");
        textArea.Document.Replace(completionSegment, code);
        if (cursor >= 0) textArea.Caret.Offset = completionSegment.Offset + cursor;
    }
}
