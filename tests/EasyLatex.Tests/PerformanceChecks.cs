using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using EasyLatex.Core;

internal static partial class Program
{
    private static void PerformanceChecks()
    {
        var pdf = Path.Combine(Root, "benchmark.pdf");
        var text = new StringBuilder("SyncTeX Version:1\n");
        for (var file = 1; file <= 10; file++) text.AppendLine($"Input:{file}:{Path.Combine(Root, $"source-{file}.tex")}");
        text.Append("Unit:1\nMagnification:1000\nX Offset:0\nY Offset:0\nContent:\n");
        for (var page = 1; page <= 500; page++)
        {
            text.AppendLine($"{{{page}");
            for (var box = 0; box < 200; box++)
                text.AppendLine($"({page % 10 + 1},{page * 200 + box}:6553600,{(box % 60 + 10) * 655360}:13107200,524288,65536");
            text.AppendLine("}");
        }
        File.WriteAllText(Path.ChangeExtension(pdf, ".synctex"), text.ToString());
        var sync = new SyncTexService();
        var watch = Stopwatch.StartNew(); sync.Load(pdf); var loadMs = watch.Elapsed.TotalMilliseconds;
        Check("perf-100000-points", sync.Points.Count == 100_000);
        var random = new Random(42); var equivalent = true;
        for (var i = 0; i < 100; i++)
        {
            var source = Path.Combine(Root, $"source-{random.Next(1, 11)}.tex"); var line = random.Next(-10, 100_100);
            var expected = sync.Points.Where(p => string.Equals(p.FilePath, source, StringComparison.OrdinalIgnoreCase)).OrderBy(p => Math.Abs(p.Line - line)).ThenBy(p => p.Width > 20 && p.Width < 500 ? 0 : 1).ThenBy(p => p.Height).FirstOrDefault();
            equivalent &= sync.Forward(source, line) == expected;
        }
        Check("perf-index-matches-reference-100-queries", equivalent);
        Check("sync-invalid-coordinates-and-missing-files", sync.Reverse(1, double.NaN, 5) is null && sync.Reverse(999, 5, 5) is null && sync.Reverse(10, -1000, -1000) is null && sync.Forward(Path.Combine(Root, "missing.tex"), 1) is null);
        var forward = Measure(() => sync.Forward(Path.Combine(Root, "source-1.tex"), 20_030));
        var reverse = Measure(() => sync.Reverse(100, 120, 220));
        var result = new { points = sync.Points.Count, pages = 500, loadMs, forward, reverse, timestamp = DateTimeOffset.UtcNow };
        File.WriteAllText(Path.Combine(Root, "performance.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PERFORMANCE " + JsonSerializer.Serialize(result));
        File.WriteAllText(Path.ChangeExtension(pdf, ".synctex"), "SyncTeX Version:1\n"); sync.Load(pdf);
        Check("sync-reload-clears-old-index", sync.Points.Count == 0 && sync.Forward(Path.Combine(Root, "source-1.tex"), 1) is null && sync.Reverse(100, 120, 220) is null);
    }
    private static object Measure(Action operation)
    {
        for (var i = 0; i < 10; i++) operation();
        var times = new double[100]; var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < times.Length; i++) { var start = Stopwatch.GetTimestamp(); operation(); times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds; }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before; Array.Sort(times);
        return new { p50Ms = times[50], p95Ms = times[95], allocatedBytesPerQuery = allocated / times.Length };
    }
}
