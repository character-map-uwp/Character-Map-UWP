using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CharacterMap.Wpf.Controls;
using CharacterMap.Wpf.Models;

namespace CharacterMap.Wpf.Services;

public static class GlyphExportService
{
    public static void Export(string path, GlyphTypeface face, GlyphItem glyph)
    {
        if (Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase)) { File.WriteAllText(path, glyph.Text); return; }
        var geometry = new DirectText { GlyphTypeface = face, CodePoint = glyph.CodePoint }.GetOutline();
        if (geometry == null || geometry.Bounds.IsEmpty || geometry.Bounds.Width <= 0 || geometry.Bounds.Height <= 0)
            throw new InvalidOperationException("该字符没有可导出的轮廓，可使用文本格式保存。");
        var bounds = geometry.Bounds;
        if (Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            var pathGeometry = PathGeometry.CreateFromGeometry(geometry);
            string data = pathGeometry.ToString(CultureInfo.InvariantCulture);
            // WPF prefixes a path with F0/F1 to encode its fill rule; SVG encodes it as an attribute.
            if (data.StartsWith("F0", StringComparison.Ordinal) || data.StartsWith("F1", StringComparison.Ordinal)) data = data[2..].TrimStart();
            string fillRule = pathGeometry.FillRule == FillRule.Nonzero ? "nonzero" : "evenodd";
            File.WriteAllText(path, FormattableString.Invariant($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{bounds.X} {bounds.Y} {bounds.Width} {bounds.Height}\"><path fill=\"black\" fill-rule=\"{fillRule}\" d=\"{data}\"/></svg>"));
        }
        else
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                double scale = 960 / Math.Max(bounds.Width, bounds.Height);
                dc.PushTransform(new TranslateTransform((1024 - bounds.Width * scale) / 2 - bounds.X * scale, (1024 - bounds.Height * scale) / 2 - bounds.Y * scale));
                dc.PushTransform(new ScaleTransform(scale, scale)); dc.DrawGeometry(Brushes.Black, null, geometry); dc.Pop(); dc.Pop();
            }
            SavePng(path, visual, 1024, 1024);
        }
    }
    public static void SavePng(string path, Visual visual, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(Math.Max(1, width), Math.Max(1, height), 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}

public sealed class FontMapPaginator(GlyphTypeface face, IReadOnlyList<GlyphItem> glyphs, string title, Size size) : DocumentPaginator
{
    private const int Columns = 8;
    private int Rows => Math.Max(1, (int)((PageSize.Height - 100) / 78));
    private int PerPage => Rows * Columns;
    public override bool IsPageCountValid => true;
    public override int PageCount => (int)Math.Ceiling(glyphs.Count / (double)PerPage);
    public override Size PageSize { get; set; } = size;
    public override IDocumentPaginatorSource Source => null!;
    public override DocumentPage GetPage(int pageNumber)
    {
        if (pageNumber < 0 || pageNumber >= PageCount) return DocumentPage.Missing;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            void Text(string text, double fontSize, Point point) => dc.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), fontSize, Brushes.Black, 1), point);
            Text(title, 20, new Point(24, 20)); Text($"{pageNumber + 1} / {PageCount}", 10, new Point(24, PageSize.Height - 28));
            double width = (PageSize.Width - 48) / Columns;
            for (int i = 0; i < PerPage && pageNumber * PerPage + i < glyphs.Count; i++)
            {
                var item = glyphs[pageNumber * PerPage + i];
                double x = 24 + i % Columns * width, y = 65 + i / Columns * 78;
                var outline = new DirectText { GlyphTypeface = face, CodePoint = item.CodePoint }.GetOutline();
                if (outline is { Bounds.IsEmpty: false } && outline.Bounds.Width > 0 && outline.Bounds.Height > 0)
                {
                    var b = outline.Bounds; double scale = Math.Min((width - 12) / b.Width, 42 / b.Height);
                    dc.PushTransform(new TranslateTransform(x + (width - b.Width * scale) / 2 - b.X * scale, y - b.Y * scale));
                    dc.PushTransform(new ScaleTransform(scale, scale)); dc.DrawGeometry(Brushes.Black, null, outline); dc.Pop(); dc.Pop();
                }
                Text(item.Code, 9, new Point(x + 6, y + 50));
            }
        }
        return new DocumentPage(visual, PageSize, new Rect(PageSize), new Rect(PageSize));
    }
}
