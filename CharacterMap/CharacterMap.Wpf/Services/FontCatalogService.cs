using System.Globalization;
using System.IO;
using System.Windows.Markup;
using System.Windows.Media;
using CharacterMap.Wpf.Models;

namespace CharacterMap.Wpf.Services;

public sealed class FontCatalogService
{
    public IReadOnlyList<FontEntry> LoadFontCollection(string path)
    {
        var result = new List<FontEntry>();
        foreach (var family in Fonts.GetFontFamilies(new Uri(Path.GetFullPath(path))))
            foreach (var face in family.GetTypefaces())
                if (face.TryGetGlyphTypeface(out var glyph)) { result.Add(new FontEntry(GetFamilyName(family), family, glyph, path)); break; }
        if (result.Count == 0) result.Add(LoadFontFile(path));
        return result;
    }
    public IReadOnlyList<FontEntry> LoadSystemFonts()
    {
        var result = new List<FontEntry>();

        foreach (FontFamily family in Fonts.SystemFontFamilies.OrderBy(GetFamilyName, StringComparer.CurrentCultureIgnoreCase))
        {
            GlyphTypeface? glyphTypeface = null;
            foreach (Typeface typeface in family.GetTypefaces().OrderBy(t => Math.Abs(t.Weight.ToOpenTypeWeight() - 400) + (t.Style == System.Windows.FontStyles.Normal ? 0 : 1000)))
            {
                if (typeface.TryGetGlyphTypeface(out glyphTypeface))
                    break;
            }

            if (glyphTypeface is not null)
                result.Add(new FontEntry(GetFamilyName(family), family, glyphTypeface));
        }

        return result;
    }

    public FontEntry LoadFontFile(string path)
    {
        var glyphTypeface = new GlyphTypeface(new Uri(path, UriKind.Absolute));
        string familyName = GetBestName(glyphTypeface.Win32FamilyNames)
            ?? Path.GetFileNameWithoutExtension(path);

        string directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("无法确定字体所在目录。");
        var baseUri = new Uri(Path.EndsInDirectorySeparator(directory) ? directory : directory + Path.DirectorySeparatorChar);
        var family = new FontFamily(baseUri, $"./#{familyName}");

        return new FontEntry(familyName, family, glyphTypeface, path);
    }

    private static string GetFamilyName(FontFamily family) =>
        GetBestName(family.FamilyNames) ?? family.Source;

    private static string? GetBestName(IDictionary<XmlLanguage, string> names)
    {
        CultureInfo ui = CultureInfo.CurrentUICulture;
        return names.TryGetValue(XmlLanguage.GetLanguage(ui.IetfLanguageTag), out string? localized) ? localized
            : names.TryGetValue(XmlLanguage.GetLanguage("en-US"), out string? english) ? english
            : names.Values.FirstOrDefault();
    }

    private static string? GetBestName(IDictionary<CultureInfo, string> names)
    {
        CultureInfo ui = CultureInfo.CurrentUICulture;
        return names.TryGetValue(ui, out string? localized) ? localized
            : names.TryGetValue(CultureInfo.GetCultureInfo("en-US"), out string? english) ? english
            : names.Values.FirstOrDefault();
    }
}
