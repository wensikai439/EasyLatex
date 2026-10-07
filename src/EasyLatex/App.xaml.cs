using System.IO;
using System.Text;
using System.Windows;

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
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        if (e.Args.Length > 0 && File.Exists(e.Args[0])) window.OpenPath(e.Args[0]);
    }
}
