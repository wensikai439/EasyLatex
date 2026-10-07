using ICSharpCode.AvalonEdit.Document;

namespace EasyLatex.Models;

// A checkpoint of the source used for the visible PDF. AvalonEdit tracks offsets
// across edits so adding lines does not invalidate navigation to that PDF.
internal sealed class CompiledSource
{
    private readonly ITextSource _snapshot;
    private readonly int[] _lines;
    public CompiledSource(DocumentSession source)
    {
        _snapshot = source.Document.CreateSnapshot();
        var text = _snapshot.Text; var lines = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
            if (text[i] == '\r') { if (i + 1 < text.Length && text[i + 1] == '\n') i++; lines.Add(i + 1); }
            else if (text[i] == '\n') lines.Add(i + 1);
        _lines = lines.ToArray();
    }
    private bool SameVersionHistory(DocumentSession source) => _snapshot.Version?.BelongsToSameDocumentAs(source.Document.Version) == true;
    public int? ForwardLine(DocumentSession source, int offset)
    {
        if (SameVersionHistory(source)) offset = source.Document.Version.MoveOffsetTo(_snapshot.Version!, offset, AnchorMovementType.BeforeInsertion);
        else if (source.Document.Text != _snapshot.Text) return null;
        offset = Math.Clamp(offset, 0, _snapshot.TextLength);
        var index = Array.BinarySearch(_lines, offset);
        return index >= 0 ? index + 1 : ~index;
    }
    public int? ReverseLine(DocumentSession source, int line)
    {
        var offset = _lines[Math.Clamp(line, 1, _lines.Length) - 1];
        if (SameVersionHistory(source)) offset = _snapshot.Version!.MoveOffsetTo(source.Document.Version, offset, AnchorMovementType.AfterInsertion);
        else if (source.Document.Text != _snapshot.Text) return null;
        return source.Document.GetLineByOffset(Math.Clamp(offset, 0, source.Document.TextLength)).LineNumber;
    }
}
