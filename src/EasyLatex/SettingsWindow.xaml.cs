using System.IO;
using System.Net.Http;
using System.Windows;
using EasyLatex.Core;
using EasyLatex.Services;
using Microsoft.Win32;

namespace EasyLatex;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private bool _deleteKey;
    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent(); _settings = settings;
        FontSizeSlider.Value = settings.EditorFontSize; AutoCompileCheck.IsChecked = settings.AutoCompile; DarkCheck.IsChecked = settings.DarkMode;
        EngineCombo.ItemsSource = new[] { "自动选择", "XeLaTeX", "pdfLaTeX", "LuaLaTeX", "Tectonic（轻量引擎）" }; EngineCombo.SelectedIndex = (int)settings.Engine;
        CompilerPath.Text = settings.CompilerDirectory; ShellEscapeCheck.IsChecked = settings.AllowShellEscape;
        OfflineCheck.IsChecked = settings.OfflineBuild;
        EndpointText.Text = settings.AiEndpoint; ModelText.Text = settings.AiModel;
        KeyStatus.Text = string.IsNullOrEmpty(CredentialStore.Read(settings.AiEndpoint)) ? "尚未保存密钥" : "已有密钥，留空可保留";
        RefreshTools();
    }
    private void RefreshTools() => DetectedTools.Text = "已检测：" + string.Join("、", CompilerService.Detect(CompilerPath.Text).Select(t => t.Label));
    private void BrowseCompiler_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择包含 xelatex.exe 等工具的文件夹" };
        if (dialog.ShowDialog(this) == true) { CompilerPath.Text = dialog.FolderName; RefreshTools(); }
    }
    private void DeleteKey_Click(object sender, RoutedEventArgs e) { _deleteKey = true; ApiKeyBox.Clear(); KeyStatus.Text = "保存设置时将删除密钥"; }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(ModelText.Text)) AiService.GetEndpoint(EndpointText.Text);
            if (_deleteKey) CredentialStore.Delete(_settings.AiEndpoint);
            if (!string.IsNullOrWhiteSpace(ApiKeyBox.Password)) CredentialStore.Write(EndpointText.Text.Trim(), ApiKeyBox.Password.Trim());
            _settings.EditorFontSize = FontSizeSlider.Value; _settings.AutoCompile = AutoCompileCheck.IsChecked == true; _settings.DarkMode = DarkCheck.IsChecked == true;
            _settings.Engine = (EngineKind)EngineCombo.SelectedIndex; _settings.CompilerDirectory = CompilerPath.Text.Trim(); _settings.AllowShellEscape = ShellEscapeCheck.IsChecked == true;
            _settings.OfflineBuild = OfflineCheck.IsChecked == true;
            _settings.AiEndpoint = EndpointText.Text.Trim(); _settings.AiModel = ModelText.Text.Trim(); SettingsStore.Save(_settings);
            DialogResult = true;
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "设置未保存", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private async void InstallEngine_Click(object sender, RoutedEventArgs e)
    {
        var button = (System.Windows.Controls.Button)sender; button.IsEnabled = false;
        try
        {
            InstallStatus.Text = "正在下载官方 Tectonic 0.17.0…";
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("EasyLatex/0.1");
            var directory = Path.Combine(SettingsStore.DirectoryPath, "tools"); Directory.CreateDirectory(directory);
            var zip = Path.Combine(directory, "tectonic.zip");
            var bytes = await client.GetByteArrayAsync("https://github.com/tectonic-typesetting/tectonic/releases/download/tectonic%400.17.0/tectonic-0.17.0-x86_64-pc-windows-msvc.zip");
            await File.WriteAllBytesAsync(zip, bytes);
            var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
            if (digest != "F61CE51F0B0ADE1015B7DE7EF368541C5424E9756ECBD0D7AF97D6D48030845F") throw new IOException("编译引擎校验失败，下载文件未被执行。请重试。");
            System.IO.Compression.ZipFile.ExtractToDirectory(zip, directory, true); File.Delete(zip);
            if (!File.Exists(Path.Combine(directory, "tectonic.exe"))) throw new IOException("下载完成，但未找到编译引擎。请重试或安装 MiKTeX。");
            CompilerPath.Text = directory; EngineCombo.SelectedIndex = (int)EngineKind.Tectonic;
            RefreshTools(); InstallStatus.Text = "安装完成。首次编译会下载需要的宏包，之后可使用缓存。";
        }
        catch (Exception ex) { InstallStatus.Text = "安装未完成：" + ex.Message; }
        finally { button.IsEnabled = true; }
    }
}
