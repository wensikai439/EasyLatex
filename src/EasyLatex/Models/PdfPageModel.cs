using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace EasyLatex.Models;

public sealed class PdfPageModel : INotifyPropertyChanged
{
    public int Index { get; init; }
    public int Number => Index + 1;
    public double PointWidth { get; init; }
    public double PointHeight { get; init; }
    private double _width;
    public double Width { get => _width; set { _width = value; Changed(nameof(Width)); Changed(nameof(Height)); } }
    public double Height => Width * PointHeight / PointWidth;
    private BitmapSource? _image;
    public BitmapSource? Image { get => _image; set { _image = value; Changed(nameof(Image)); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed(string name) => PropertyChanged?.Invoke(this, new(name));
}
