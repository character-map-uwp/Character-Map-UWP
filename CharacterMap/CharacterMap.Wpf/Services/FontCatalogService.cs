using System.Globalization;
using System.IO;
using System.Windows.Markup;
using System.Windows.Media;
using CharacterMap.Wpf.Models;

namespace CharacterMap.Wpf.Services;

public sealed class FontCatalogService
{
    private readonly Dictionary<string, IReadOnlyList<FontEntry>> _webFonts = new(StringComparer.Ordinal);
    private static readonly Lazy<string> WebFontDirectory = new(() =>
    {
        string directory = Path.Combine(Path.GetTempPath(), "CharacterMap.Wpf", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try { Directory.Delete(directory, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        };
        return directory;
    });
    public IReadOnlyList<FontEntry> LoadFontCollection(string path)
    {
        path = Path.GetFullPath(path);
        // Inspect the signature as well as the extension, including renamed web fonts.
        using (var stream = File.OpenRead(path))
        {
            Span<byte> signature = stackalloc byte[4];
            stream.ReadExactly(signature);
            if (signature.SequenceEqual("wOF2"u8) || Path.GetExtension(path).Equals(".woff2", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("WOFF2 解码尚未移植，请使用原始 TTF/OTF 文件。");
            if (signature.SequenceEqual("wOFF"u8) || Path.GetExtension(path).Equals(".woff", StringComparison.OrdinalIgnoreCase))
                return LoadWebFont(path);
        }
        return LoadNativeCollection(path, path);
    }

    private IReadOnlyList<FontEntry> LoadWebFont(string path)
    {
        byte[] bytes = FontBinary.ReadFile(path);
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        string key = path + "|" + hash;
        if (_webFonts.TryGetValue(key, out var cached)) return cached;
        byte[] decoded = WebFontDecoder.Decode(bytes);
        string target = Path.Combine(WebFontDirectory.Value, Guid.NewGuid().ToString("N") + (decoded[0] == (byte)'O' ? ".otf" : ".ttf"));
        File.WriteAllBytes(target, decoded);
        try
        {
            var fonts = LoadNativeCollection(target, path);
            _webFonts.Add(key, fonts);
            return fonts;
        }
        catch
        {
            try { File.Delete(target); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    private IReadOnlyList<FontEntry> LoadNativeCollection(string path, string originalPath)
    {
        var result = new List<FontEntry>();
        foreach (var family in Fonts.GetFontFamilies(new Uri(Path.GetFullPath(path))))
            foreach (var face in family.GetTypefaces())
                if (face.TryGetGlyphTypeface(out var glyph)) { result.Add(new FontEntry(GetFamilyName(family), family, glyph, originalPath)); break; }
        if (result.Count == 0)
        {
            var font = LoadFontFile(path);
            result.Add(new FontEntry(font.DisplayName, font.Family, font.GlyphTypeface, originalPath));
        }
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
        path = Path.GetFullPath(path);
        if (Path.GetExtension(path).Equals(".woff", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".woff2", StringComparison.OrdinalIgnoreCase))
            return LoadFontCollection(path)[0];
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
