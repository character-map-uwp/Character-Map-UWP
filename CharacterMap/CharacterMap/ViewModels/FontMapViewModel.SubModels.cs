using CommunityToolkit.Mvvm.Input;
using Microsoft.Graphics.Canvas.Text;
using System.Collections;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace CharacterMap.ViewModels;

public partial class FaceAnalysisModel : ViewModelBase
{
    private CMFontFace _face;

    [ObservableProperty] IReadOnlyList<DWriteFontAxis> _variationAxis;

    [ObservableProperty] IReadOnlyList<Suggestion> _rampOptions;

    public bool HasFontOptions { get; }

    public bool ShowColorGlyphs { get; }

    public FontAnalysis Analysis { get; }

    public bool IsMDL2Font { get; }

    public FontFamily FontFamily { get; }

    public GlyphCollection Glyphs { get; }

    private Task<Uri> _loadingTask = null;

    [RelayCommand]
    public Task<Uri> LoadGlyphFontAsync()
    {
        if (Glyphs.FontUri is not null)
            return Task.FromResult(Glyphs.FontUri);

        if (_face is null)
            return Task.FromResult<Uri>(null);

        return _loadingTask ??= LoadGlyphFontInternalAsync();
    }

    private async Task<Uri> LoadGlyphFontInternalAsync()
    {
        await Glyphs.LoadMoreItemsAsync(10).AsTask();
        return Glyphs.FontUri;
    }

    public FaceAnalysisModel(CMFontFace face)
    {
        if (face is null)
            return;

        _face = face;

        FontAnalysis analysis = face.GetAnalysis();
        TypographyAnalyzer.PrepareSearchMap(face, analysis);
        analysis.ResetVariableAxis();

        FontFamily = new(face.Source);
        IsMDL2Font = FontFinder.IsMDL2(face);
        HasFontOptions = analysis.ContainsVectorColorGlyphs || face.HasXamlTypographyFeatures;
        ShowColorGlyphs = face.DirectWriteProperties.IsColorFont;

        Analysis = analysis;
        Glyphs = new(face);

        UpdateVariations();
        UpdateRampOptions();
    }

    public void UpdateVariations()
    {
        VariationAxis =
            Analysis?.Axis?.Where(a => (a.Attribute & DWriteFontAxisAttribute.Variable) != 0).ToList()
            ?? [];
    }

    public void UpdateRampOptions()
    {
        RampOptions = GetRampOptions(_face);
    }

    private IReadOnlyList<Suggestion> GetRampOptions(CMFontFace variant)
    {
        if (variant == null)
            return [];

        var list = GlyphService.GetRampOptions();

        if (variant?.TryGetSampleText() is String s)
            list.Insert(0, new Suggestion(Localization.Get("SuggestOptionSample/Text"), s));

        if (Unicode.ContainsRange(variant, UnicodeRange.Emoticons))
        {
            string emoji = "😂😍😭💁👍💋🐱🦉🌺🌲🍓🍕🎂🏰🏠🚄🚒🛫🛍";
            if (!list.Any(s => s.Text == emoji))
                list.Add(new Suggestion(Localization.Get("SuggestOptionEmoji/Text"), emoji));
        }

        return list;
    }

}

[DebuggerDisplay("TV {DisplayName}, IsMapped: {IsVariationMapped}")]
public class TypographyVariation
{
    public static TypographyVariation None { get; } = new();

    public TypographyVariation() { }

    public TypographyVariation(TypographyFeatureInfo feature)
    {
        Feature = feature;
    }

    public TypographyFeatureInfo Feature { get; set; } = TypographyFeatureInfo.None;

    /// <summary>
    /// If <see cref="IsVariationMapped"/> is true, this represents the Unicode character mapping for the typography variation.
    /// </summary>
    public int FaceCharacterMapping { get; set; } = -1;

    /// <summary>
    /// If true, the typographic variation glyph is also mapped as a Unicode character in the font face. 
    /// This means that the variation can be accessed directly as a character, rather than just as a typography feature.
    /// </summary>
    public bool IsVariationMapped => FaceCharacterMapping >= 0;

    public bool IsNone => Feature == TypographyFeatureInfo.None;

    public string DisplayName => Feature?.DisplayName;

    public override string ToString() => DisplayName;

    public override bool Equals(object obj) => obj is TypographyVariation other && Equals(Feature, other.Feature);

    public override int GetHashCode() => Feature?.GetHashCode() ?? 0;
}

public partial class CharacterAnalysisModel : ViewModelBase, IEquatable<CharacterAnalysisModel>
{
    private NativeInterop _interop = Utils.GetInterop();

    public Character Char { get; }

    [ObservableProperty] bool _isSvgChar;
    [ObservableProperty] CanvasTextLayoutAnalysis _analysis;
    [ObservableProperty] List<TypographyVariation> _variations;
    [ObservableProperty] UnihanData _unihanData;
    [ObservableProperty] IReadOnlyList<ushort> _glyphIndices;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMultipleGlyphs), nameof(HasGlyphs), nameof(GlyphHeader))]
    IReadOnlyList<GlyphCharacter> _glyphs;

    public bool HasMultipleGlyphs => GlyphIndices is { Count: > 1 };
    public bool HasGlyphs => GlyphIndices is { Count: > 0 };
    public string GlyphHeader => HasMultipleGlyphs ? Localization.Get("TxtGlyphsHeader") : Localization.Get("TxtGlyphHeader");
    //public string GlyphSummary => GlyphIndices switch
    //{
    //    null or { Count: 0 } => null,
    //    [ushort single] => $"Glyph {single}",
    //    _ => $"Glyphs: {string.Join(", ", GlyphIndices)}"
    //};

    private readonly FontMapViewModel _vm;
    private readonly CMFontFace face;

    public CharacterAnalysisModel(CMFontFace face, Character c, FontMapViewModel vm)
    {
        if (c is null || face is null || vm is null)
            return; // we are empty shell;

        this.face = face;
        Char = c;
        _vm = vm;
        Analysis = GetCharAnalysis(c, face);
        Variations = TypographyAnalyzer.GetCharacterVariations(face, c);
        IsSvgChar = Analysis.GlyphFormats.Contains(GlyphImageFormat.Svg);
        UnihanData = GlyphService.GetUnihanData(c.UnicodeIndex);
        UpdateGlyphIndices();
    }

    public void UpdateAnalysis(TypographyFeatureInfo typography = null)
    {
        Analysis = GetCharAnalysis(Char, face, typography);
        IsSvgChar = Analysis.GlyphFormats.Contains(GlyphImageFormat.Svg);
        UpdateGlyphIndices();
    }

    private void UpdateGlyphIndices()
    {
        if (Char is GlyphCharacter gc)
        {
            GlyphIndices = [gc.GlyphIndex];
            Glyphs = [gc];
        }
        else if (Analysis?.GlyphIndices is { Count: > 0 } indices)
        {
            GlyphIndices = [.. indices];
            Glyphs = [.. indices.Select(i => new GlyphCharacter(i))];
        }
        else if (Analysis?.Indicies is { Length: > 0 } runIndices)
        {
            List<ushort> list = [.. runIndices.SelectMany(r => r)];
            GlyphIndices = list;
            Glyphs = [.. list.Select(i => new GlyphCharacter(i))];
        }
        else
        {
            GlyphIndices = [];
            Glyphs = [];
        }
    }

    public CanvasTextLayoutAnalysis GetCharAnalysis(Character c, CMFontFace face, TypographyFeatureInfo typography = null)
    {
        if (c is GlyphCharacter gc)
            return _interop.AnalyzeGlyphLayout(face.Face, gc.GlyphIndex);

        using CanvasTextLayout layout = new(Utils.CanvasDevice, $"{c.Char}", new()
        {
            FontSize = (float)Core.Converters.GetFontSize(Settings.GridSize),
            FontFamily = face.Source,
            FontStretch = face.DirectWriteProperties.Stretch,
            FontWeight = face.DirectWriteProperties.Weight,
            FontStyle = face.DirectWriteProperties.Style,
            HorizontalAlignment = CanvasHorizontalAlignment.Left,
        }, Settings.GridSize, Settings.GridSize);

        // This doesn't work if it's set during the property constructor.
        // Leave it as a separate line.
        layout.Options = CanvasDrawTextOptions.EnableColorFont;

        ApplyEffectiveTypography(layout, typography);
        return _interop.AnalyzeCharacterLayout(layout);
    }

    private void ApplyEffectiveTypography(CanvasTextLayout layout, TypographyFeatureInfo typography = null)
    {
        using CanvasTypography type = GetEffectiveTypography(typography);
        layout.SetTypography(0, 1, type);
    }

    private CanvasTypography GetEffectiveTypography(TypographyFeatureInfo typography = null)
    {
        if (typography == null)
            typography = _vm.SelectedTypography.Feature;

        CanvasTypography typo = new();
        if (typography != null && typography.Feature != CanvasTypographyFeatureName.None)
            typo.AddFeature(typography.Feature, 1u);

        return typo;
    }

    #region Equality Overrides

    public override bool Equals(object obj)
    {
        return Equals(obj as CharacterAnalysisModel);
    }

    public bool Equals(CharacterAnalysisModel other)
    {
        return other is not null &&
               EqualityComparer<Character>.Default.Equals(Char, other.Char) &&
               EqualityComparer<FontMapViewModel>.Default.Equals(_vm, other._vm) &&
               EqualityComparer<CMFontFace>.Default.Equals(face, other.face);
    }

    public override int GetHashCode()
    {
        int hashCode = -831823616;
        hashCode = hashCode * -1521134295 + EqualityComparer<Character>.Default.GetHashCode(Char);
        hashCode = hashCode * -1521134295 + EqualityComparer<FontMapViewModel>.Default.GetHashCode(_vm);
        hashCode = hashCode * -1521134295 + EqualityComparer<CMFontFace>.Default.GetHashCode(face);
        return hashCode;
    }

    public static bool operator ==(CharacterAnalysisModel left, CharacterAnalysisModel right)
    {
        return EqualityComparer<CharacterAnalysisModel>.Default.Equals(left, right);
    }

    public static bool operator !=(CharacterAnalysisModel left, CharacterAnalysisModel right)
    {
        return !(left == right);
    }

    #endregion
}

public partial class FontMapSearchModel : ViewModelBase
{
    private Debouncer _debouncer { get; } = new();
    private ConcurrencyToken.ConcurrencyTokenGenerator _tokenFactory { get; } = new();
    private CMFontFace _face;
    private IReadOnlyList<UnicodeRangeModel> _categories;

    [ObservableProperty] string _query;
    [ObservableProperty] IEnumerable _results;
    [ObservableProperty] bool _isGrouped;
    [ObservableProperty] bool _isSearching;

    partial void OnQueryChanged(string value)
    {
        DebounceSearch(value, Settings.InstantSearchDelay, SearchSource.AutoProperty);
    }
    public void SetContext(CMFontFace face, IReadOnlyList<UnicodeRangeModel> categories)
    {
        _face = face;
        _categories = categories;
    }
    public void Clear()
    {
        _tokenFactory.GenerateToken(); // Invalidate inflight searches
        //Query = string.Empty;
        Results = null;
        IsGrouped = false;
        IsSearching = false;
    }
    public void DebounceSearch(string query, int delayMilliseconds = 500, SearchSource from = SearchSource.AutoProperty)
    {
        if (from == SearchSource.AutoProperty && !Settings.UseInstantSearch)
            return;
        if (from == SearchSource.ManualSubmit || delayMilliseconds <= 0)
            Search(query);
        else
            _debouncer.Debounce(delayMilliseconds, () => Search(query));
    }
    public async void Search(string query)
    {
        if (_face is null || string.IsNullOrWhiteSpace(query))
        {
            Results = null;
            IsGrouped = false;
            return;
        }
        ConcurrencyToken token = _tokenFactory.GenerateToken();
        IsSearching = true;
        try
        {
            IReadOnlyList<IGlyphData> results = await GlyphService.SearchAsync(query, _face);
            if (!token.IsValid())
                return;
            IsGrouped = false;
            if (results is null || results.Count == 0)
            {
                Results = null;
                return;
            }
            // Group search results if filtering is active on glyph categories
            if (_categories is not null && _categories.Any(c => !c.IsSelected))
            {
                if (SearchResultsGroup.CreateGroups(results, _categories) is { } groups
                    && groups.HasHiddenResults)
                {
                    IsGrouped = true;
                    Results = groups;
                    return;
                }
            }
            Results = results;
        }
        finally
        {
            if (token.IsValid())
                IsSearching = false;
        }
    }
}