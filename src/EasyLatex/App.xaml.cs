using System.IO;
using System.Text;
using System.Windows;
using EasyLatex.Core;
using EasyLatex.Services;

namespace EasyLatex;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show("操作未完成：" + args.Exception.Message + "\n\n你的编辑内容仍保留在窗口中。", "EasyLatex", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };
        var verify = e.Args.Length > 0 && e.Args[0] is "--verify-portable" or "--verify-portable-default";
        var profile = e.Args.Length > 0 && e.Args[0] == "--profile-startup";
        var startupWatch = System.Diagnostics.Stopwatch.StartNew();
        var output = verify || profile ? Path.GetFullPath(e.Args.ElementAtOrDefault(1) ?? "portable-verification") : "";
        if (verify || profile)
        {
            Environment.SetEnvironmentVariable("EASYLATEX_DATA_DIR", Path.Combine(output, "user-data"));
            Environment.SetEnvironmentVariable("TECTONIC_CACHE_DIR", Path.Combine(output, "compiler-cache"));
            SettingsStore.Save(new() { Engine = EngineKind.Tectonic, OfflineBuild = e.Args[0] != "--verify-portable-default" });
        }
        var window = new MainWindow();
        MainWindow = window;
        if (verify || profile)
        {
            window.EnableVerificationMode();
            window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -5000; window.Top = -5000; window.ShowInTaskbar = false;
            window.Loaded += async (_, _) =>
            {
                if (verify) await PortableVerification.RunAsync(window, output);
                else
                {
                    await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    Directory.CreateDirectory(output);
                    await File.WriteAllTextAsync(Path.Combine(output, "ready.json"), System.Text.Json.JsonSerializer.Serialize(new { uiReadyMs = startupWatch.Elapsed.TotalMilliseconds }));
                    await Task.Delay(1500);
                    using var process = System.Diagnostics.Process.GetCurrentProcess(); process.Refresh();
                    await File.WriteAllTextAsync(Path.Combine(output, "profile.json"), System.Text.Json.JsonSerializer.Serialize(new { workingSetMiB = process.WorkingSet64 / 1048576.0, privateMiB = process.PrivateMemorySize64 / 1048576.0, managedMiB = GC.GetTotalMemory(false) / 1048576.0, timestamp = DateTimeOffset.UtcNow }));
                    Shutdown();
                }
            };
        }
        window.Show();
        if (!verify && !profile && e.Args.Length > 0 && File.Exists(e.Args[0])) window.OpenPath(e.Args[0]);
    }
}
