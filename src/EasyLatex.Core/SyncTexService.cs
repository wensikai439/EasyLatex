using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace EasyLatex.Core;

public sealed class SyncTexService
{
    public List<SyncPoint> Points { get; } = [];
    public void Load(string pdf)
    {
        Points.Clear();
        var basePath = Path.ChangeExtension(pdf, ".synctex");
        var file = File.Exists(basePath + ".gz") ? basePath + ".gz" : basePath;
        if (!File.Exists(file)) return;
        using var input = File.OpenRead(file);
        using Stream stream = file.EndsWith(".gz") ? new GZipStream(input, CompressionMode.Decompress) : input;
        using var data = new MemoryStream();
        stream.CopyTo(data);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string text;
        try { text = new UTF8Encoding(false, true).GetString(data.ToArray()); }
        catch (DecoderFallbackException) { text = Encoding.GetEncoding(0).GetString(data.ToArray()); }
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
            if (int.Parse(m.Groups[2].Value) > 0) Points.Add(new(source, int.Parse(m.Groups[2].Value), page, x, y - Number(6) * factor, Math.Max(width, 1), Math.Max(height, 9)));
        }
    }
    public SyncPoint? Forward(string source, int line) => Points.Where(p => string.Equals(p.FilePath, Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))
        .OrderBy(p => Math.Abs(p.Line - line)).ThenBy(p => p.Width > 500 ? 1 : 0).ThenBy(p => p.Height).FirstOrDefault();
    public SyncPoint? Reverse(int page, double x, double y)
    {
        static double Gap(double at, double start, double length) => at < start ? start - at : at > start + length ? at - start - length : 0;
        return Points.Where(p => p.Page == page && p.Width < 500 && p.Height < 80)
            .OrderBy(p => Gap(y, p.Y, p.Height) * 4 + Gap(x, p.X, p.Width)).ThenBy(p => p.Height).FirstOrDefault();
    }
}
