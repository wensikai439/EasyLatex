using System.Diagnostics;
using System.Text;

namespace EasyLatex.Core;

public sealed record ProcessResult(int ExitCode, string Output);
public static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(string executable, IEnumerable<string> arguments,
        string workingDirectory, Action<string>? onLine = null, CancellationToken token = default,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        var info = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in arguments) info.ArgumentList.Add(arg);
        if (environment is not null) foreach (var pair in environment) info.Environment[pair.Key] = pair.Value;
        using var process = new Process { StartInfo = info };
        process.Start();
        using var cancel = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
        var output = new StringBuilder();
        var gate = new object();
        async Task Drain(StreamReader reader)
        {
            while (await reader.ReadLineAsync(token) is { } line)
            {
                lock (gate) { if (output.Length < 1_000_000) output.AppendLine(line); }
                onLine?.Invoke(line);
            }
        }
        await Task.WhenAll(Drain(process.StandardOutput), Drain(process.StandardError), process.WaitForExitAsync(token));
        token.ThrowIfCancellationRequested();
        return new(process.ExitCode, output.ToString());
    }
}
