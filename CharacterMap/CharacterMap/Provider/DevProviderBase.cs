namespace CharacterMap.Provider;

public partial class DevProviderBase
{
    /// <summary>
    /// Creates dev providers for a glyph.
    /// Register all known providers here.
    /// </summary>
    /// <param name="o"></param>
    /// <param name="c"></param>
    /// <returns></returns>
    public static IReadOnlyList<DevProviderBase> GetProviders(CharacterRenderingOptions o, Character c)
    {
        return new List<DevProviderBase>
        {
            new DevProviderNone(o, c),
            new UnicodeDevProvider(o, c),
            new XamlDevProvider(o, c),
            new CSharpDevProvider(o, c),
            new CSharpWinUI3DevProvider(o, c),
            new VBDevProvider(o, c),
            new CppCxDevProvider(o, c),
            new CppWinrtDevProvider(o, c),
            new CppWinrtWinUI3DevProvider(o, c),
            new XamarinFormsDevProvider(o,c)
        };
    }

    private static List<KeyValuePair<GeometryCacheEntry, string>> _geometryCache { get; } = new();

    /// <summary>
    /// Creates an SVG / XAML path syntax compatible string representing the filled geometry
    /// of a glyph.
    /// </summary>
    /// <param name="c"></param>
    /// <param name="o"></param>
    /// <returns></returns>
    public static string GetOutlineGeometry(
       Character c,
       CharacterRenderingOptions o)
    {
        /* 
         * We use a cache because creating geometry is currently a little bit more expensive than it needs to be 
         * (we're not actually reading the data from the font, but using D2D to "Draw" the geometry to a custom sink)
         * and we might be creating it multiple times for a single glyph depending on how our dev providers are
         * configured, so a small cache will help performance
         */

        if (_geometryCache.FirstOrDefault(p => p.Key is GeometryCacheEntry e && e.Options == o && e.Character == c) is { } pair
            && pair.Value != null)
            return pair.Value;


        string pathIconData = null;
        if (o.Face != null)
        {
            // We use a font size of 20 as this metrically maps to the size of SegoeMDL2 icons used
            // in FontIcon / SymbolIcon controls.
            (pathIconData, _) = ExportManager.GetGeometry(c, o with { FontSize = 20 });
            _geometryCache.Add(KeyValuePair.Create(new GeometryCacheEntry(c, o), pathIconData));

            // Keep the cache to a certain size
            while (_geometryCache.Count > 10)
                _geometryCache.RemoveAt(0);
        }

        return pathIconData;
    }
}

/// <summary>
/// Base class for Dev providers.
/// Should enable lazy evaluation of dev values by only creating
/// values when calling GetOptions()/GetContextOptions()
/// </summary>
public abstract partial class DevProviderBase
{
    protected static List<DevOption> DefaultUWPOptions { get; } = new()
    {
        new ("TxtXamlCode/Header", null),
        new ("TxtFontIcon/Header", null),
        new ("TxtPathIcon/Text", null),
        new ("TxtSymbolIcon/Header", null),
    };

    public string ResourceKey { get; }
    public string DisplayName { get; protected init; }

    public DevProviderType Type { get; }

    protected NativeInterop Interop { get; }

    protected CharacterRenderingOptions Options { get; }

    protected Character Character { get; }

    private IReadOnlyList<DevOption> _contextOptions = null;

    private IReadOnlyList<DevOption> _previewPaneOptions = null;

    public DevProviderBase(CharacterRenderingOptions r, Character character)
    {
        Options = r;
        Character = character;
        Interop = Utils.GetInterop();
        Type = GetDevProviderType();
        ResourceKey = $"Provider{Type}";
    }

    protected abstract DevProviderType GetDevProviderType();

    /// <summary>
    /// Gets options for display in the context menu when right clicking a glyph.
    /// </summary>
    /// <returns></returns>
    protected abstract IReadOnlyList<DevOption> OnGetContextOptions();

    /// <summary>
    /// Gets options for display under the character preview window.
    /// Try not to have more than 4 options here to prevent the UI becoming too cluttered.
    /// </summary>
    /// <returns></returns>
    protected abstract IReadOnlyList<DevOption> OnGetOptions();

    /// <summary>
    /// Returns all the possible (shell) values this provider can return.
    /// </summary>
    /// <returns></returns>
    public virtual IReadOnlyList<DevOption> GetAllOptions() => DefaultUWPOptions;

    /// <summary>
    /// Gets options for display in the context menu when right clicking a glyph.
    /// </summary>
    /// <returns></returns>
    public IReadOnlyList<DevOption> GetContextOptions() => _contextOptions ??= OnGetContextOptions();

    /// <summary>
    /// Gets options for display under the character preview window.
    /// Try not to have more than 4 options here to prevent the UI becoming too cluttered.
    /// </summary>
    /// <returns></returns>
    public IReadOnlyList<DevOption> GetOptions() => _previewPaneOptions ??= OnGetOptions();

    /// <summary>
    /// Maps a typography feature to its property name and platform-specific values.
    /// </summary>
    /// <param name="PropertyName">A typography property name.</param>
    /// <param name="XamlValue">A XAML property value.</param>
    /// <param name="CodeValue">A code value as it appears in C# code.
    /// Use {0} as a namespace placeholder for enum types.</param>
    protected record TypographyMapping(string PropertyName, string XamlValue, string CodeValue);

    /// <summary>
    /// Gets a typography mapping for the specified character rendering options.
    /// </summary>
    /// <param name="options">The character rendering options that specify the
    /// typography feature to map.</param>
    /// <returns>
    /// A <see cref="TypographyMapping"/> instance for the specified character
    /// rendering options, or null if no mapping is available.
    /// </returns>
    protected static TypographyMapping GetTypographyMapping(CharacterRenderingOptions options)
    {
        var typo = options.DefaultTypography;
        if (typo is null)
            return null;

        return (DWriteTypographyFeatureName)typo.Feature switch
        {
            // Stylistic Sets (bool)
            DWriteTypographyFeatureName.StylisticSet1  => new("StylisticSet1",  "True", "true"),
            DWriteTypographyFeatureName.StylisticSet2  => new("StylisticSet2",  "True", "true"),
            DWriteTypographyFeatureName.StylisticSet3  => new("StylisticSet3",  "True", "true"),
            DWriteTypographyFeatureName.StylisticSet4  => new("StylisticSet4",  "True", "true"),
            DWriteTypographyFeatureName.StylisticSet5  => new("StylisticSet5",  "True", "true"),
            DWriteTypographyFeatureName.StylisticSet6  => new("StylisticSet6",  "True", "true"),
            DWriteTypographyFeatureName.StylisticSet7  => new("StylisticSet7",  "True", "true"),
            DWriteTypographyFeatureName.StylisticSet8  => new("StylisticSet8",  "True", "true"),
            DWriteTypographyFeatureName.StylisticSet9  => new("StylisticSet9",  "True", "true"),
            DWriteTypographyFeatureName.StylisticSet10 => new("StylisticSet10", "True", "true"),
            DWriteTypographyFeatureName.StylisticSet11 => new("StylisticSet11", "True", "true"),
            DWriteTypographyFeatureName.StylisticSet12 => new("StylisticSet12", "True", "true"),
            DWriteTypographyFeatureName.StylisticSet13 => new("StylisticSet13", "True", "true"),
            DWriteTypographyFeatureName.StylisticSet14 => new("StylisticSet14", "True", "true"),
            DWriteTypographyFeatureName.StylisticSet15 => new("StylisticSet15", "True", "true"),
            DWriteTypographyFeatureName.StylisticSet16 => new("StylisticSet16", "True", "true"),
            DWriteTypographyFeatureName.StylisticSet17 => new("StylisticSet17", "True", "true"),
            DWriteTypographyFeatureName.StylisticSet18 => new("StylisticSet18", "True", "true"),
            DWriteTypographyFeatureName.StylisticSet19 => new("StylisticSet19", "True", "true"),
            DWriteTypographyFeatureName.StylisticSet20 => new("StylisticSet20", "True", "true"),

            // Capitals (FontCapitals)
            DWriteTypographyFeatureName.SmallCapitalsFromCapitals  => new("Capitals", "AllSmallCaps",  "{0}.UI.Xaml.FontCapitals.AllSmallCaps"),
            DWriteTypographyFeatureName.SmallCapitals              => new("Capitals", "SmallCaps",     "{0}.UI.Xaml.FontCapitals.SmallCaps"),
            DWriteTypographyFeatureName.PetiteCapitalsFromCapitals => new("Capitals", "AllPetiteCaps", "{0}.UI.Xaml.FontCapitals.AllPetiteCaps"),
            DWriteTypographyFeatureName.PetiteCapitals             => new("Capitals", "PetiteCaps",    "{0}.UI.Xaml.FontCapitals.PetiteCaps"),
            DWriteTypographyFeatureName.Unicase                    => new("Capitals", "Unicase",       "{0}.UI.Xaml.FontCapitals.Unicase"),
            DWriteTypographyFeatureName.Titling                    => new("Capitals", "Titling",       "{0}.UI.Xaml.FontCapitals.Titling"),

            // Numeral Style (FontNumeralStyle)
            DWriteTypographyFeatureName.LiningFigures   => new("NumeralStyle", "Lining",   "{0}.UI.Xaml.FontNumeralStyle.Lining"),
            DWriteTypographyFeatureName.OldStyleFigures => new("NumeralStyle", "OldStyle", "{0}.UI.Xaml.FontNumeralStyle.OldStyle"),

            // Numeral Alignment (FontNumeralAlignment)
            DWriteTypographyFeatureName.ProportionalFigures => new("NumeralAlignment", "Proportional", "{0}.UI.Xaml.FontNumeralAlignment.Proportional"),
            DWriteTypographyFeatureName.TabularFigures      => new("NumeralAlignment", "Tabular",      "{0}.UI.Xaml.FontNumeralAlignment.Tabular"),

            // Variants (FontVariants)
            DWriteTypographyFeatureName.Superscript         => new("Variants", "Superscript","{0}.UI.Xaml.FontVariants.Superscript"),
            DWriteTypographyFeatureName.Subscript           => new("Variants", "Subscript",  "{0}.UI.Xaml.FontVariants.Subscript"),
            DWriteTypographyFeatureName.Ordinals            => new("Variants", "Ordinal",    "{0}.UI.Xaml.FontVariants.Ordinal"),
            DWriteTypographyFeatureName.ScientificInferiors => new("Variants", "Inferior",   "{0}.UI.Xaml.FontVariants.Inferior"),
            DWriteTypographyFeatureName.RubyNotationForms   => new("Variants", "Ruby",       "{0}.UI.Xaml.FontVariants.Ruby"),

            // Fraction (FontFraction)
            DWriteTypographyFeatureName.Fractions => new("Fraction", "Slashed", "{0}.UI.Xaml.FontFraction.Slashed"),

            // Swashes (int)
            DWriteTypographyFeatureName.Swash           => new("StandardSwashes",   "1", "1"),
            DWriteTypographyFeatureName.ContextualSwash => new("ContextualSwashes", "1", "1"),

            // Alternates (int)
            DWriteTypographyFeatureName.StylisticAlternates => new("StylisticAlternates", "1", "1"),
            // Alternates (bool)
            DWriteTypographyFeatureName.ContextualAlternates => new("ContextualAlternates", "True", "true"),

            // Forms (int)
            DWriteTypographyFeatureName.AlternateAnnotationForms => new("AnnotationAlternates", "1", "1"),
            // Forms (bool)
            DWriteTypographyFeatureName.CaseSensitiveForms => new("CaseSensitiveForms",   "True", "true"),
            DWriteTypographyFeatureName.HistoricalForms    => new("HistoricalForms",      "True", "true"),
            DWriteTypographyFeatureName.ExpertForms        => new("EastAsianExpertForms", "True", "true"),

            // East Asian Forms (FontEastAsianLanguage)
            DWriteTypographyFeatureName.HojoKanjiForms       => new("EastAsianLanguage", "HojoKanji",        "{0}.UI.Xaml.FontEastAsianLanguage.HojoKanji"),
            DWriteTypographyFeatureName.Jis04Forms           => new("EastAsianLanguage", "Jis04",            "{0}.UI.Xaml.FontEastAsianLanguage.Jis04"),
            DWriteTypographyFeatureName.Jis78Forms           => new("EastAsianLanguage", "Jis78",            "{0}.UI.Xaml.FontEastAsianLanguage.Jis78"),
            DWriteTypographyFeatureName.Jis83Forms           => new("EastAsianLanguage", "Jis83",            "{0}.UI.Xaml.FontEastAsianLanguage.Jis83"),
            DWriteTypographyFeatureName.Jis90Forms           => new("EastAsianLanguage", "Jis90",            "{0}.UI.Xaml.FontEastAsianLanguage.Jis90"),
            DWriteTypographyFeatureName.NlcKanjiForms        => new("EastAsianLanguage", "NlcKanji",         "{0}.UI.Xaml.FontEastAsianLanguage.NlcKanji"),
            DWriteTypographyFeatureName.SimplifiedForms      => new("EastAsianLanguage", "Simplified",       "{0}.UI.Xaml.FontEastAsianLanguage.Simplified"),
            DWriteTypographyFeatureName.TraditionalForms     => new("EastAsianLanguage", "Traditional",      "{0}.UI.Xaml.FontEastAsianLanguage.Traditional"),
            DWriteTypographyFeatureName.TraditionalNameForms => new("EastAsianLanguage", "TraditionalNames", "{0}.UI.Xaml.FontEastAsianLanguage.TraditionalNames"),

            //// East Asian Widths (FontEastAsianWidths)
            DWriteTypographyFeatureName.FullWidth          => new("EastAsianWidths", "Full",         "{0}.UI.Xaml.FontEastAsianWidths.Full"),
            DWriteTypographyFeatureName.HalfWidth          => new("EastAsianWidths", "Half",         "{0}.UI.Xaml.FontEastAsianWidths.Half"),
            DWriteTypographyFeatureName.ProportionalWidths => new("EastAsianWidths", "Proportional", "{0}.UI.Xaml.FontEastAsianWidths.Proportional"),
            DWriteTypographyFeatureName.QuarterWidths      => new("EastAsianWidths", "Quarter",      "{0}.UI.Xaml.FontEastAsianWidths.Quarter"),
            DWriteTypographyFeatureName.ThirdWidths        => new("EastAsianWidths", "Third",        "{0}.UI.Xaml.FontEastAsianWidths.Third"),

            // Ligatures (bool)
            DWriteTypographyFeatureName.StandardLigatures      => new("StandardLigatures",      "True", "true"),
            DWriteTypographyFeatureName.ContextualLigatures    => new("ContextualLigatures",    "True", "true"),
            DWriteTypographyFeatureName.HistoricalLigatures    => new("HistoricalLigatures",    "True", "true"),
            DWriteTypographyFeatureName.DiscretionaryLigatures => new("DiscretionaryLigatures", "True", "true"),

            // Other (bool)
            DWriteTypographyFeatureName.CapitalSpacing     => new("CapitalSpacing",       "True", "true"),
            DWriteTypographyFeatureName.Kerning            => new("Kerning",              "True", "true"),
            DWriteTypographyFeatureName.MathematicalGreek  => new("MathematicalGreek",    "True", "true"),
            DWriteTypographyFeatureName.SlashedZero        => new("SlashedZero",          "True", "true"),

            _ => null
        };
    }
}
