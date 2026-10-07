using System.ComponentModel;
using System.Windows.Media.Imaging;
using EasyLatex.Core;

namespace EasyLatex.Models;

public sealed class PdfPageModel : INotifyPropertyChanged
{
    public int Index { get; init; }
    public int Number => Index + 1;
    public double PointWidth { get; init; }
    public double PointHeight { get; init; }
    private double _width;
    public double Width { get => _width; set { _width = value; Changed(nameof(Width)); Changed(nameof(Height)); NotifyMarker(); } }
    public double Height => Width * PointHeight / PointWidth;
    private BitmapSource? _image;
    public BitmapSource? Image { get => _image; set { _image = value; Changed(nameof(Image)); } }
    private SyncPoint? _marker;
    public bool HasMarker => _marker is not null;
    public double MarkerLeft => _marker?.X / PointWidth * Width ?? 0;
    public double MarkerTop => _marker?.Y / PointHeight * Height ?? 0;
    public double MarkerWidth => Math.Max(30, (_marker?.Width ?? 0) / PointWidth * Width);
    public double MarkerHeight => Math.Max(12, (_marker?.Height ?? 0) / PointHeight * Height);
    public void Mark(SyncPoint? point) { _marker = point; NotifyMarker(); }
    private void NotifyMarker() { foreach (var name in new[] { nameof(HasMarker), nameof(MarkerLeft), nameof(MarkerTop), nameof(MarkerWidth), nameof(MarkerHeight) }) Changed(name); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed(string name) => PropertyChanged?.Invoke(this, new(name));
}
