namespace CharacterMap.Models;

public class FileNameWriterArgs
{
    public ExportOptions Options { get; init; }
    public Character Character { get; init; }
    public string Extension { get; init; }
}

public partial class FileNameWriter
{
    public string Match { get; init; }
    public string Description { get; init; }
    public string Example { get; init; }

    public Func<FileNameWriterArgs, string> Func { get; init; }

    public string Process(FileNameWriterArgs a, string input)
    {
        if (Func is null)
            return input;

        return input.Replace(Match, Func(a), StringComparison.InvariantCultureIgnoreCase);
    }

    public override string ToString()
    {
        return String.Format(Localization.Get("FileNameWriterStringFormat"), Description, Example);
    }




    /*
     * "All" list is created in source gen from all FileNameWriter
     * properties in this class
     */

    public static FileNameWriter Family { get; } = new()
    {
        Match = "{family}",
        Description = Localization.Get("FileNameWriterFamilyDesc"),
        Example = "Segoe UI",
        Func = a => a.Options.Font.Name
    };

    public static FileNameWriter Face { get; } = new()
    {
        Match = "{face}",
        Description = Localization.Get("FileNameWriterFaceDesc"),
        Example = "Regular",
        Func = a => a.Options?.Options?.Variant?.PreferredName
    };

    public static FileNameWriter CharacterDescription { get; } = new()
    {
        Match = "{desc}",
        Description = Localization.Get("FileNameWriterCharDescDesc"),
        Example = "Latin Capital Letter A",
        Func = a => a.Options?.Options?.Variant?.GetDescription(a.Character) ?? a.Character.UnicodeString
    };

    public static FileNameWriter UnicodeHex { get; } = new()
    {
        Match = "{hex}",
        Description = Localization.Get("FileNameWriterUnicodeHexDesc"),
        Example = "U+F0041",
        Func = a => a.Character.UnicodeString
    };

    public static FileNameWriter UnicodeCodepoint { get; } = new()
    {
        Match = "{index}",
        Description = Localization.Get("FileNameWriterUnicodeCPDesc"),
        Example = "65",
        Func = a => a.Character.UnicodeIndex.ToString()
    };

    // "\" is invalid character to be used in filenames
    //public static FileNameWriter UTF16 { get; } = new()
    //{
    //    Match = "{utf16}",
    //    Description = "UTF-16 value",
    //    Example = "\\uF0DF",
    //    Func = a => a.Character.GetClipboardString()
    //};

    public static FileNameWriter XamlGlyph { get; } = new()
    {
        Match = "{xamlGlyph}",
        Description = Localization.Get("FileNameWriterXamlGlyphDesc"),
        Example = "&#x0041;",
        Func = a => $"&#x{a.Character.UnicodeIndex.ToString("x4").ToUpper()};"
    };

    public static FileNameWriter PixelSize { get; } = new()
    {
        Match = "{pixels}",
        Description = Localization.Get("FileNameWriterPixelSizeDesc"),
        Example = "1024",
        Func = a => $"{a.Options.PreferredSize}"
    };

    public static FileNameWriter FontVersion { get; } = new()
    {
        Match = "{ver}",
        Description = Localization.Get("FileNameWriterFontVerDesc"),
        Example = "5.2",
        Func = a =>
        {
            if (Utils.TryGetVersion(a.Options.Options.Variant, out double version))
                return version.ToString();

            return string.Empty;
        }
    };

    public static FileNameWriter Extension { get; } = new()
    {
        Match = "{ext}",
        Description = Localization.Get("FileNameWriterExtensionDesc"),
        Example = "png"
    };

}
