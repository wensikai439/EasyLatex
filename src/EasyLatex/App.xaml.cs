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
        var output = verify ? Path.GetFullPath(e.Args.ElementAtOrDefault(1) ?? "portable-verification") : "";
        if (verify)
        {
            Environment.SetEnvironmentVariable("EASYLATEX_DATA_DIR", Path.Combine(output, "user-data"));
            Environment.SetEnvironmentVariable("TECTONIC_CACHE_DIR", Path.Combine(output, "compiler-cache"));
            SettingsStore.Save(new() { Engine = EngineKind.Tectonic, OfflineBuild = e.Args[0] != "--verify-portable-default" });
        }
        var window = new MainWindow();
        MainWindow = window;
        if (verify)
        {
            window.EnableVerificationMode();
            window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -5000; window.Top = -5000; window.ShowInTaskbar = false;
            window.Loaded += async (_, _) => await PortableVerification.RunAsync(window, output);
        }
        window.Show();
        if (!verify && e.Args.Length > 0 && File.Exists(e.Args[0])) window.OpenPath(e.Args[0]);
    }
}
