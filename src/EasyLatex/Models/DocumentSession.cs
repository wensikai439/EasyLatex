using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit.Document;

namespace EasyLatex.Models;

public sealed class DocumentSession : INotifyPropertyChanged
{
    public string? FilePath { get; set; }
    public TextDocument Document { get; } = new();
    public Encoding Encoding { get; private set; } = new UTF8Encoding(false, true);
    private bool _forcedDirty;
    public bool IsDirty => _forcedDirty || !Document.UndoStack.IsOriginalFile;
    private bool _isActive;
    public bool IsActive { get => _isActive; set { _isActive = value; PropertyChanged?.Invoke(this, new(nameof(IsActive))); } }
    private DateTime _savedWriteTime;
    private string? _savedPath;
    public string Title => (FilePath is null ? "未命名.tex" : Path.GetFileName(FilePath)) + (IsDirty ? " •" : "");
    public event PropertyChangedEventHandler? PropertyChanged;
    public DocumentSession(string text = "", string? path = null)
    {
        FilePath = path;
        Document.Text = text;
        Document.UndoStack.ClearAll();
        Document.UndoStack.MarkAsOriginalFile();
        Document.Changed += (_, _) => PropertyChanged?.Invoke(this, new(nameof(Title)));
        Document.UndoStack.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(Document.UndoStack.IsOriginalFile))
            { PropertyChanged?.Invoke(this, new(nameof(Title))); }
        };
    }
    public static DocumentSession Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > 20_000_000) throw new IOException("文件超过 20 MB，建议拆分为章节文件后编辑。");
        Encoding encoding = new UTF8Encoding(false, true);
        var offset = 0;
        if (bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) { encoding = new UTF8Encoding(true, true); offset = 3; }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE })) { encoding = new UnicodeEncoding(false, true, true); offset = 2; }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xFE, 0xFF })) { encoding = new UnicodeEncoding(true, true, true); offset = 2; }
        else
        {
            var header = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 2000));
            var m = Regex.Match(header, @"(?im)^\s*%\s*!\s*TeX\s+encoding\s*=\s*(\S+)");
            if (m.Success) encoding = Encoding.GetEncoding(m.Groups[1].Value, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        }
        try { return new DocumentSession(encoding.GetString(bytes, offset, bytes.Length - offset), Path.GetFullPath(path)) { Encoding = encoding, _savedPath = Path.GetFullPath(path), _savedWriteTime = File.GetLastWriteTimeUtc(path) }; }
        catch (DecoderFallbackException) { throw new IOException("无法可靠识别文件编码。请将文件转换为 UTF-8，或在头部声明 % !TeX encoding = GBK。原文件没有改动。"); }
    }
    public void Save()
    {
        if (FilePath is null) throw new InvalidOperationException("请先选择保存位置。");
        if (string.Equals(FilePath, _savedPath, StringComparison.OrdinalIgnoreCase) && File.Exists(FilePath) && File.GetLastWriteTimeUtc(FilePath) != _savedWriteTime)
            throw new IOException("文件已被其他程序修改。请另存为保留当前编辑内容，再重新打开原文件检查差异。");
        if (!IsDirty && string.Equals(FilePath, _savedPath, StringComparison.OrdinalIgnoreCase) && File.Exists(FilePath)) return;
        var content = Encoding.GetPreamble().Concat(Encoding.GetBytes(Document.Text)).ToArray();
        var temporary = FilePath + ".easylatex.tmp";
        try
        {
            File.WriteAllBytes(temporary, content);
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null); else File.Move(temporary, FilePath);
            _forcedDirty = false; Document.UndoStack.MarkAsOriginalFile();
            _savedPath = FilePath; _savedWriteTime = File.GetLastWriteTimeUtc(FilePath);
            PropertyChanged?.Invoke(this, new(nameof(Title)));
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void MarkDirty() { _forcedDirty = true; PropertyChanged?.Invoke(this, new(nameof(Title))); }
}
