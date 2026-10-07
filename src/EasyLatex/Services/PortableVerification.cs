using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EasyLatex.Core;

namespace EasyLatex.Services;

// A developer smoke check executed by the actual published executable.
internal static class PortableVerification
{
    public static async Task RunAsync(MainWindow window, string output)
    {
        var success = false;
        string? failure = null;
        var templates = new List<object>();
        try
        {
            Directory.CreateDirectory(output);
            success = true;
            foreach (var (name, content) in new[] { ("English", Templates.Article), ("中文模板", Templates.Chinese), ("Slides", Templates.Beamer) })
            {
                var source = Path.Combine(output, name + ".tex"); await File.WriteAllTextAsync(source, content);
                window.OpenPath(source); await window.CompileAsync();
                var ok = window.Pages.Count > 0 && window.Pages[0].Image is not null && ((TextBlock)window.FindName("StatusText")).Text.StartsWith("编译完成") && ((TextBlock)window.FindName("EngineStatus")).Text == "Tectonic";
                success &= ok; templates.Add(new { name, success = ok, status = ((TextBlock)window.FindName("StatusText")).Text });
                await File.WriteAllTextAsync(Path.Combine(output, "progress.txt"), name + ": " + ((TextBlock)window.FindName("StatusText")).Text);
            }
            var root = (FrameworkElement)window.Content; root.UpdateLayout();
            var dpi = VisualTreeHelper.GetDpi(root);
            var bitmap = new RenderTargetBitmap((int)(root.ActualWidth * dpi.DpiScaleX), (int)(root.ActualHeight * dpi.DpiScaleY), 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32); bitmap.Render(root);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(output, "portable.png")); png.Save(file);
        }
        catch (Exception ex) { success = false; failure = ex.ToString(); }
        finally
        {
            Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { success, failure, templates, offline = SettingsStore.Load().OfflineBuild, enginePath = CompilerService.FindTool("tectonic"), runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory(), nativeDirectories = AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES"), tools = CompilerService.Detect().Select(t => t.Label), pages = window.Pages.Count, status = ((TextBlock)window.FindName("StatusText")).Text, timestamp = DateTimeOffset.UtcNow }, new JsonSerializerOptions { WriteIndented = true }));
            Application.Current.Shutdown(success ? 0 : 1);
        }
    }
}
