namespace EasyLatex.Core;

public static class CompilerCache
{
    private static readonly SemaphoreSlim Gate = new(1);
    public static async Task SeedAsync(CancellationToken token)
    {
        var seed = Path.Combine(AppContext.BaseDirectory, "tools", "compiler-cache");
        if (!Directory.Exists(seed)) return;
        var destination = Environment.GetEnvironmentVariable("TECTONIC_CACHE_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EasyLatex", "compiler-cache");
        Environment.SetEnvironmentVariable("TECTONIC_CACHE_DIR", destination);
        await Gate.WaitAsync(token);
        try
        {
            foreach (var source in Directory.EnumerateFiles(seed, "*", SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();
                var target = Path.Combine(destination, Path.GetRelativePath(seed, source));
                if (File.Exists(target)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    await using (var input = File.OpenRead(source))
                    await using (var output = File.Create(temporary)) await input.CopyToAsync(output, token);
                    try { File.Move(temporary, target); } catch (IOException) when (File.Exists(target)) { }
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }
        finally { Gate.Release(); }
    }
}
