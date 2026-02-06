// Ignore Spelling: cfi

using Microsoft.Graphics.Canvas.Text;
using System.Globalization;

namespace CharacterMap.Core;

public record FaceMetadataInfo(string Key, string[] Values, CanvasFontInformation Info)
{
    public string Value => string.Join(", ", Values);
}

/// <summary>
/// Represents an instance of a FontFace
/// </summary>
[System.Diagnostics.DebuggerDisplay("{FamilyName} {PreferredName}")]
public partial class CMFontFace : IDisposable
{
    /* Using a character cache avoids a lot of unnecessary allocations */
    private static Dictionary<int, Character> _characters { get; } = [];

    private IReadOnlyList<NamedUnicodeRange> _ranges = null;
    private IReadOnlyList<FaceMetadataInfo> _fontInformation = null;
    private IReadOnlyList<TypographyFeatureInfo> _typographyFeatures = null;
    private IReadOnlyList<TypographyFeatureInfo> _xamlTypographyFeatures = null;
    private FontAnalysis _analysis = null;
    private FaceMetadataInfo _designLangRawSearch = null;

    public IReadOnlyList<FaceMetadataInfo> FontInformation => _fontInformation ??= GetFontInformation();

    public IReadOnlyList<TypographyFeatureInfo> TypographyFeatures => _typographyFeatures ??= LoadTypographyFeatures();

    /// <summary>
    /// Supported XAML typographer features for A SINGLE GLYPH. 
    /// Does not include features like Alternates which are used for strings of text.
    /// </summary>
    public IReadOnlyList<TypographyFeatureInfo> XamlTypographyFeatures => _xamlTypographyFeatures ??= LoadTypographyFeatures(true);

    public bool HasXamlTypographyFeatures => XamlTypographyFeatures.Count > 0;

    public CanvasFontFace FontFace => Face.FontFace;

    public string PreferredName { get; private set; }

    public IReadOnlyList<Character> Characters { get; private set; }

    public double CharacterHash { get; private set; }

    public bool IsImported { get; }

    public string FileName { get; }

    public string FamilyName { get; }

    public CanvasUnicodeRange[] UnicodeRanges => Face.GetUnicodeRanges();

    private Panose _panose = null;
    public Panose Panose => _panose ??= PanoseParser.Parse(Face.Properties);

    public DWriteProperties DirectWriteProperties { get; }

    /// <summary>
    /// File-system path for DWrite / XAML to construct a font for use in this application
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// A FontFamily source for XAML that includes a custom fall-back font.
    /// This results in XAML *only* rendering the characters included in the font.
    /// Use when you may have a scenario where characters not inside a font's glyph
    /// range might be displayed, otherwise use <see cref="Source"/> for better performance.
    /// </summary>
    public string DisplaySource => $"{Source}, /Assets/AdobeBlank.otf#Adobe Blank";

    /// <summary>
    /// Font source that external applications should use to display this font in XAML
    /// </summary>
    public string XamlFontSource =>
        (IsImported ? $"/Assets/Fonts/{FileName}#{FamilyName}" : Source);

    public DWriteFontFace Face { get; }

    public CMFontFace(DWriteFontFace face, StorageFile file)
    {
        DWriteProperties dwProps = face.Properties;
        Face = face;
        FamilyName = dwProps.FamilyName;

        if (file != null)
        {
            IsImported = true;
            FileName = file.Name;
            Source = $"{FontFinder.GetAppPath(file)}#{dwProps.FamilyName}";
        }
        else
        {
            Source = dwProps.FamilyName;
        }

        string name = dwProps.FaceName;
        if (String.IsNullOrEmpty(name))
            name = Utils.GetVariantDescription(face);

        DirectWriteProperties = dwProps;
        PreferredName = name;
    }

    public string GetProviderName()
    {
        //if (!String.IsNullOrEmpty(DirectWriteProperties.RemoteProviderName))
        //    return DirectWriteProperties.RemoteProviderName;

        if (IsImported)
            return Localization.Get("InstallTypeImported");

        return Localization.Get($"DWriteSource{DirectWriteProperties.Source}");
    }

    public IReadOnlyList<NamedUnicodeRange> GetRanges()
    {
        return _ranges ??=
            GetCharacters().GroupBy(c => c.Range).Select(g => g.Key).ToList();
    }

    public IReadOnlyList<Character> GetCharacters()
    {
        if (Characters == null)
        {
            List<Character> characters = [];
            foreach (var range in UnicodeRanges)
            {
                CharacterHash += range.First;
                CharacterHash += range.Last;

                int last = (int)range.Last;
                for (int i = (int)range.First; i <= last; i++)
                {
                    if (!_characters.TryGetValue(i, out Character c))
                    {
                        c = new Character((uint)i);
                        _characters[i] = c;
                    }

                    characters.Add(c);
                }
            }
            Characters = characters;
        }

        return Characters;
    }

    public int GetGlyphIndex(Character c) => Face.GetGlyphIndice(c.UnicodeIndex);

    public uint[] GetGlyphUnicodeIndexes() => GetCharacters().Select(c => c.UnicodeIndex).ToArray();

    public FontAnalysis GetAnalysis() => _analysis ??= TypographyAnalyzer.Analyze(this);

    public string QuickFilePath => GetAnalysisInternal().FilePath;

    /// <summary>
    /// Load an analysis without a glyph search map. Callers later using the cached analysis and expecting a search map should
    /// take care to ensure it's created by manually calling <see cref="TypographyAnalyzer.PrepareSearchMap(CMFontFace, FontAnalysis)"/>
    /// </summary>
    /// <returns></returns>
    private FontAnalysis GetAnalysisInternal() => _analysis ??= TypographyAnalyzer.Analyze(this, false);

    /// <summary>
    /// Used temporarily to allow insider builds to access COLRv1. Do not use elsewhere. Very expensive.
    /// </summary>
    public bool SupportsCOLRv1Rendering => Utils.Supports23H2 && DirectWriteProperties.IsColorFont && GetAnalysisInternal().SupportsCOLRv1;
    
    public bool ContainsCOLRV0Glyphs => DirectWriteProperties.IsColorFont && GetAnalysisInternal().COLRVersion == 0;
   
    public bool ContainsSVGGlyphs => DirectWriteProperties.IsColorFont && GetAnalysisInternal().HasSVGGlyphs;
    
    public bool ContainsBitmapGlyphs => DirectWriteProperties.IsColorFont && GetAnalysisInternal().HasBitmapGlyphs;

    /// <summary>
    /// Hack used for QuickCompare - we show ALL colour fonts using manual DirectWrite rendering (using DirectText control) rather than 
    /// XAML TextBlock. We cannot use the flag above to filter only COLRv1 fonts as the FontAnalysis object requires actually opening and 
    /// manually parsing the font file headers - too expensive an operation to perform when scrolling the entire font list on the UI thread.
    /// /// </summary>
    public bool SupportsColourRendering => Utils.Supports23H2 && DirectWriteProperties.IsColorFont;

    public string TryGetSampleText() => ReadInfoKey(CanvasFontInformation.SampleText)?.Value;

    /// <summary>
    /// Attempts to return the value of <see cref="CanvasFontInformation.FullName"/>. If it fails,
    /// <see cref="PreferredName"/> is returned instead.
    /// </summary>
    /// <returns></returns>
    public string TryGetFullName() => TryGetInfo(CanvasFontInformation.FullName)?.Value ?? PreferredName;

    public bool HasDesignScriptTag(string tag)
    {
        TryGetInfo(CanvasFontInformation.DesignScriptLanguageTag);
        return _designLangRawSearch?.Values.Contains(tag, StringComparer.OrdinalIgnoreCase) ?? false;
    }

    public bool CouldContainUnihan() => UnicodeRanges.Any(r => Unicode.UNIHAN_IDX >= r.First && Unicode.UNIHAN_IDX <= r.Last);




    //------------------------------------------------------
    //
    // Searching
    //
    //------------------------------------------------------

    public Dictionary<Character, string> SearchMap { get; set; }

    public string GetDescription(Character c, bool allowUnihan = false)
    {
        if (SearchMap == null
            || !SearchMap.TryGetValue(c, out string mapping)
            || string.IsNullOrWhiteSpace(mapping))
        {
            string name = GlyphService.GetCharacterDescription(c.UnicodeIndex, this);
            if (string.IsNullOrWhiteSpace(name)
                && allowUnihan
                && Unicode.CouldBeUnihan(c.UnicodeIndex)
                && GlyphService.GetUnihanData(c.UnicodeIndex)?.Definition
                    is { } def)
                name = def.Description;

            return name;
        }


        return GlyphService.TryGetAGLFNName(mapping);
    }




    //------------------------------------------------------
    //
    // Internal
    //
    //------------------------------------------------------

    private IReadOnlyList<TypographyFeatureInfo> LoadTypographyFeatures(bool isXaml = false)
    {
        var features = TypographyAnalyzer.GetSupportedTypographyFeatures(this);

        var xaml = features.Where(f => TypographyBehavior.IsXamlSingleGlyphSupported(f.Feature)).ToList();
        if (xaml.Count > 0)
            xaml.Insert(0, TypographyFeatureInfo.None);
        _xamlTypographyFeatures = xaml;

        if (features.Count > 0)
            features.Insert(0, TypographyFeatureInfo.None);
        _typographyFeatures = features;

        return isXaml ? _xamlTypographyFeatures : _typographyFeatures;
    }

    private List<FaceMetadataInfo> GetFontInformation()
         => INFORMATIONS.Select(ReadInfoKey)
                        .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Value))
                        .Append(GetEmbeddingMetadata())
                        .ToList();

    /// <summary>
    /// Reads an info key from the underlying DWriteFontFace
    /// </summary>
    /// <param name="fontFace"></param>
    /// <param name="info"></param>
    /// <returns></returns>
    private FaceMetadataInfo ReadInfoKey(CanvasFontInformation info)
    {
        var infos = Face.GetInformationalStrings(info);
        if (infos.Count == 0)
            return null;

        // Get localised field name
        var name = Localization.Get($"CanvasFontInformation{info}") ?? info.Humanise();

        // Get localised value name
        var dic = infos.ToDictionary(k => k.Key, k => k.Value);
        if (infos.TryGetValue(CultureInfo.CurrentCulture.Name, out string value)
            || infos.TryGetValue("en-us", out value))
            return new(name, new string[1] { value }, info);

        string[] values = null;

        // For design tag, cache the tag for later use in search
        if (info is CanvasFontInformation.DesignScriptLanguageTag
            && _designLangRawSearch is null)
        {
            _designLangRawSearch = new(name, infos.Select(i => UnicodeScriptTags.GetBaseTag(i.Value)).ToArray(), info);
        }

        if (info is CanvasFontInformation.DesignScriptLanguageTag or CanvasFontInformation.SupportedScriptLanguageTag)
            values = infos.Select(i => UnicodeScriptTags.GetName(i.Value)).ToArray();

        return new(
            name,
            values ?? infos.Select(i => i.Value).ToArray(),
            info);
    }

    /// <summary>
    /// Attempts to return a cached info key, or load it from scratch.
    /// </summary>
    /// <param name="cfi"></param>
    /// <returns></returns>
    public FaceMetadataInfo TryGetInfo(CanvasFontInformation cfi)
    {
        if (_fontInformation is not null && _fontInformation.FirstOrDefault(p => p.Info == cfi)
            is { } info)
            return info;

        if (ReadInfoKey(cfi) is { } faceInfo)
            return faceInfo;

        return null;
    }

    FaceMetadataInfo GetEmbeddingMetadata()
    {
        StringBuilder sb = Utils.BuilderPool.Request();
        try
        {
            foreach (var info in Face.GetEmbeddingType().ToString().Split(',').Select(s => s.Trim()))
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine();
                }

                sb.Append(Localization.Get($"EmbeddingType{info}"));
            }

            return new(
                Localization.Get("CanvasFontInformationEmbeddingRights"),
                [sb.ToString()],
                CanvasFontInformation.LicenseDescription);
        }
        finally
        {
            Utils.BuilderPool.Return(sb);
        }


    }




    /* .NET */

    public void Dispose() => FontFace?.Dispose();

    public override string ToString() => PreferredName;
}


public partial class CMFontFace
{
    public static CMFontFace CreateDefault(DWriteFontFace face)
    {
        return new CMFontFace(face, null)
        {
            PreferredName = "",
            Characters = [ new(0) ]
        };
    }

    private static CanvasFontInformation[] INFORMATIONS { get; } = {
        CanvasFontInformation.FullName,
        CanvasFontInformation.Description,
        CanvasFontInformation.VersionStrings,
        CanvasFontInformation.DesignScriptLanguageTag,
        CanvasFontInformation.SupportedScriptLanguageTag,
        CanvasFontInformation.Designer,
        CanvasFontInformation.DesignerUrl,
        CanvasFontInformation.FontVendorUrl,
        CanvasFontInformation.Manufacturer,
        CanvasFontInformation.Trademark,
        CanvasFontInformation.CopyrightNotice,
        CanvasFontInformation.LicenseInfoUrl,
        CanvasFontInformation.LicenseDescription,
    };
}
