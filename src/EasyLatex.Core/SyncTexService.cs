using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace EasyLatex.Core;

public sealed class SyncTexService
{
    private readonly List<SyncPoint> _points = [];
    public IReadOnlyList<SyncPoint> Points => _points;
    private record SourceLine(int Line, SyncPoint Point, int Order);
    private readonly Dictionary<string, SourceLine[]> _sources = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, SyncPoint[]> _pages = [];
    public void Load(string pdf)
    {
        _points.Clear(); _sources.Clear(); _pages.Clear();
        var basePath = Path.ChangeExtension(pdf, ".synctex");
        var file = File.Exists(basePath + ".gz") ? basePath + ".gz" : basePath;
        if (!File.Exists(file)) return;
        using var input = File.OpenRead(file);
        using Stream stream = file.EndsWith(".gz") ? new GZipStream(input, CompressionMode.Decompress) : input;
        using var data = new MemoryStream();
        stream.CopyTo(data);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string text;
        var bytes = data.GetBuffer().AsSpan(0, (int)data.Length);
        try { text = new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { text = Encoding.GetEncoding(0).GetString(bytes); }
        using var reader = new StringReader(text);
        var sources = new Dictionary<int, string>();
        double unit = 1, magnification = 1000, xOffset = 0, yOffset = 0;
        var page = 0;
        var node = new Regex(@"^[\[\(hvxkg$](\d+),(\d+)(?:,\d+)?:\s*(-?\d+),(-?\d+)(?::(-?\d+)(?:,(-?\d+),(-?\d+))?)?", RegexOptions.Compiled);
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("Input:"))
            {
                var first = line.IndexOf(':', 6);
                if (first > 6 && int.TryParse(line[6..first], out var tag))
                {
                    var path = line[(first + 1)..];
                    if (!Path.IsPathRooted(path)) path = Path.Combine(Path.GetDirectoryName(pdf)!, "..", "..", path);
                    sources[tag] = Path.GetFullPath(path);
                }
                continue;
            }
            double Value() => double.Parse(line[(line.IndexOf(':') + 1)..], CultureInfo.InvariantCulture);
            if (line.StartsWith("Unit:")) { unit = Value(); continue; }
            if (line.StartsWith("Magnification:")) { magnification = Value(); continue; }
            if (line.StartsWith("X Offset:")) { xOffset = Value(); continue; }
            if (line.StartsWith("Y Offset:")) { yOffset = Value(); continue; }
            if (line.StartsWith('{') && int.TryParse(line[1..], out var p)) { page = p; continue; }
            var m = node.Match(line);
            if (page < 1 || !m.Success || !sources.TryGetValue(int.Parse(m.Groups[1].Value), out var source)) continue;
            var factor = unit * magnification / 1000 / 65536 * 72 / 72.27;
            double Number(int i) => m.Groups[i].Success ? double.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture) : 0;
            var x = (Number(3) + xOffset) * factor;
            var y = (Number(4) + yOffset) * factor;
            var width = Math.Abs(Number(5) * factor);
            var height = Math.Abs((Number(6) + Number(7)) * factor);
            if (int.Parse(m.Groups[2].Value) > 0) _points.Add(new(source, int.Parse(m.Groups[2].Value), page, x, y - Number(6) * factor, Math.Max(width, 1), Math.Max(height, 9)));
        }
        foreach (var source in _points.Select((point, order) => new SourceLine(point.Line, point, order)).GroupBy(p => p.Point.FilePath, StringComparer.OrdinalIgnoreCase))
            _sources[source.Key] = source.GroupBy(p => p.Line).Select(line => line.OrderBy(p => Preferred(p.Point)).ThenBy(p => p.Point.Height).First()).OrderBy(p => p.Line).ToArray();
        foreach (var group in _points.Where(p => p.Width < 500 && p.Height < 80 && Path.GetExtension(p.FilePath).Equals(".tex", StringComparison.OrdinalIgnoreCase)).GroupBy(p => p.Page))
            _pages[group.Key] = group.ToArray();
    }
    private static int Preferred(SyncPoint point) => point.Width > 20 && point.Width < 500 ? 0 : 1;
    public SyncPoint? Forward(string source, int line)
    {
        if (!_sources.TryGetValue(Path.GetFullPath(source), out var lines) || lines.Length == 0) return null;
        var low = 0; var high = lines.Length;
        while (low < high) { var middle = (low + high) / 2; if (lines[middle].Line < line) low = middle + 1; else high = middle; }
        if (low == 0) return lines[0].Point;
        if (low == lines.Length) return lines[^1].Point;
        var before = lines[low - 1]; var after = lines[low];
        var distance = ((long)line - before.Line).CompareTo((long)after.Line - line);
        if (distance != 0) return distance < 0 ? before.Point : after.Point;
        var preference = Preferred(before.Point).CompareTo(Preferred(after.Point));
        if (preference != 0) return preference < 0 ? before.Point : after.Point;
        var height = before.Point.Height.CompareTo(after.Point.Height);
        return height != 0 ? (height < 0 ? before.Point : after.Point) : (before.Order < after.Order ? before.Point : after.Point);
    }
    public SyncPoint? Reverse(int page, double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !_pages.TryGetValue(page, out var points)) return null;
        static double Gap(double at, double start, double length) => at < start ? start - at : at > start + length ? at - start - length : 0;
        SyncPoint? best = null; var score = double.MaxValue;
        foreach (var point in points)
        {
            var distance = Gap(y, point.Y, point.Height) * 4 + Gap(x, point.X, point.Width);
            if (distance < score || distance == score && point.Height < best!.Height) { best = point; score = distance; }
        }
        // Avoid sending clicks in distant margins to unrelated source lines.
        return score <= 96 ? best : null;
    }
}
