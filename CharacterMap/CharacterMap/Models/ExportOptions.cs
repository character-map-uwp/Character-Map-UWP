using Windows.UI;

namespace CharacterMap.Models;

public record ExportOptions
{
    public const string DefaultTemplate = "{family} {face} - {desc}.{ext}";

    public CMFontFamily Font { get; init; }
    public CharacterRenderingOptions Options { get; init; }

    public string FileNameTemplate { get; init; }

    public double PreferredSize { get; init; }
    public ExportFormat PreferredFormat { get; init; }
    public ExportStyle PreferredStyle { get; init; }
    public Color PreferredColor { get; init; }
    public StorageFolder TargetFolder { get; init; }
    public bool SkipEmptyGlyphs { get; init; }

    public ExportOptions() { }

    public ExportOptions(ExportFormat format, ExportStyle style)
    {
        PreferredSize = ResourceHelper.AppSettings.PngSize;
        PreferredFormat = format;
        PreferredColor = style switch
        {
            ExportStyle.White => Colors.White,
            _ => Colors.Black
        };
        PreferredStyle = style;
    }

    public string GetFileName(Character c, string ext)
    {
        string s = (FileNameTemplate ?? ResourceHelper.AppSettings.FileNameTemplate).Trim();
        FileNameWriterArgs a = new() { Character = c, Extension = ext, Options = this };

        foreach (var w in FileNameWriter.All)
            s = w.Process(a, s);

        s = s.Replace("{ext}", ext);
        s = Utils.RemoveInvalidChars(s);

        if (string.IsNullOrWhiteSpace(s))
            s = DefaultTemplate;

        return s;
    }
}
