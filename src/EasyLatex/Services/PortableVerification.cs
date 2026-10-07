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
        try
        {
            Directory.CreateDirectory(output);
            var source = Path.Combine(output, "portable-demo.tex"); await File.WriteAllTextAsync(source, Templates.Article);
            window.OpenPath(source); await window.CompileAsync();
            success = window.Pages.Count > 0 && window.Pages[0].Image is not null && ((TextBlock)window.FindName("StatusText")).Text.StartsWith("编译完成") && ((TextBlock)window.FindName("EngineStatus")).Text == "Tectonic";
            var root = (FrameworkElement)window.Content; root.UpdateLayout();
            var dpi = VisualTreeHelper.GetDpi(root);
            var bitmap = new RenderTargetBitmap((int)(root.ActualWidth * dpi.DpiScaleX), (int)(root.ActualHeight * dpi.DpiScaleY), 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32); bitmap.Render(root);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(output, "portable.png")); png.Save(file);
        }
        catch (Exception ex) { failure = ex.ToString(); }
        finally
        {
            Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { success, failure, runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory(), nativeDirectories = AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES"), tools = CompilerService.Detect().Select(t => t.Label), pages = window.Pages.Count, status = ((TextBlock)window.FindName("StatusText")).Text, timestamp = DateTimeOffset.UtcNow }, new JsonSerializerOptions { WriteIndented = true }));
            Application.Current.Shutdown(success ? 0 : 1);
        }
    }
}
