using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage.Streams;

namespace EasyLatex.Services;

public sealed class PdfService : IDisposable
{
    private PdfDocument? _document;
    private InMemoryRandomAccessStream? _source;
    private byte[]? _bytes;
    private readonly SemaphoreSlim _renderGate = new(1);
    public uint PageCount => _document?.PageCount ?? 0;
    public string? Path { get; private set; }
    public async Task LoadAsync(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path);
        var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream)) { writer.WriteBytes(bytes); await writer.StoreAsync(); await writer.FlushAsync(); writer.DetachStream(); }
        stream.Seek(0);
        var document = await PdfDocument.LoadFromStreamAsync(stream);
        await _renderGate.WaitAsync();
        try { _document = document; _source?.Dispose(); _source = stream; _bytes = bytes; Path = path; }
        finally { _renderGate.Release(); }
    }
    public (double Width, double Height) Size(int index)
    {
        using var page = _document!.GetPage((uint)index);
        return (page.Size.Width, page.Size.Height);
    }
    public Task ExportAsync(string path) => File.WriteAllBytesAsync(path, _bytes ?? throw new InvalidOperationException("请先成功编译文档，再导出 PDF。"));
    public async Task<BitmapSource?> RenderAsync(int index, double width, CancellationToken token)
    {
        await _renderGate.WaitAsync(token);
        try
        {
            if (_document is null || index >= _document.PageCount) return null;
            using var page = _document.GetPage((uint)index);
            using var stream = new InMemoryRandomAccessStream();
            var renderWidth = (uint)Math.Clamp(width, 200, 2400);
            await page.RenderToStreamAsync(stream, new PdfPageRenderOptions { DestinationWidth = renderWidth }).AsTask(token);
            token.ThrowIfCancellationRequested();
            // WinRT's destination is in DIPs; normalize the bitmap to our pixel cache size.
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = (int)renderWidth; bitmap.StreamSource = stream.AsStreamForRead(); bitmap.EndInit(); bitmap.Freeze();
            return bitmap;
        }
        finally { _renderGate.Release(); }
    }
    public void Dispose() { _source?.Dispose(); _renderGate.Dispose(); }
}
