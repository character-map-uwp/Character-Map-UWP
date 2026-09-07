using CharacterMap.Controls;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace CharacterMap.ViewModels;

public partial class PrintViewModel : ViewModelBase
{
    protected override bool TrackAnimation => true;

    public CMFontFace Font { get; set; }

    public TypographyFeatureInfo Typography { get; set; }

    public FontFamily FontFamily { get; set; }

    private bool _hideWhitespace = true;
    public bool HideWhitespace
    {
        get => _hideWhitespace;
        set
        {
            if (value != _hideWhitespace)
            {
                _hideWhitespace = value;
                UpdateCharacters();
                OnPropertyChanged();
            }
        }
    }

    [ObservableProperty] bool _showMargins;
    [ObservableProperty] bool _showBorders;
    [ObservableProperty] bool _showColorGlyphs;
    [ObservableProperty] double _glyphSize = 70d;
    [ObservableProperty] double _horizontalMargin = 44d;
    [ObservableProperty] double _verticalMargin = 44d;
    [ObservableProperty] GlyphAnnotation _annotation = GlyphAnnotation.None;
    [ObservableProperty] PrintLayout _layout = PrintLayout.Grid;
    [ObservableProperty] Orientation _orientation = Orientation.Vertical;

    public IReadOnlyList<Character> Characters { get; set; }
    [ObservableProperty] IList<UnicodeRangeModel> _categories;
    [ObservableProperty] int _firstPage = 1;
    [ObservableProperty]int _pagesToPrint = 50;

    public bool IsPortrait => Orientation == Orientation.Vertical;

    internal CharacterGridViewTemplateSettings GetTemplateSettings()
    {
        return new CharacterGridViewTemplateSettings
        {
            Size = GlyphSize,
            ShowColorGlyphs = ShowColorGlyphs,
            Annotation = GlyphAnnotation.None,
            Typography = Typography,
            FontFamily = FontFamily,
            FontFace = Font.Face
        };
    }

    public void UpdateCategories(IList<UnicodeRangeModel> value)
    {
        _categories = value;
        UpdateCharacters();
        OnPropertyChanged(nameof(Categories));
    }

    private void UpdateCharacters()
    {
        // Fast path : all characters;
        if (!Categories.Any(c => !c.IsSelected) && !HideWhitespace)
        {
            Characters = Font.Characters;
            return;
        }

        // Filter characters
        Characters = Unicode.FilterCharacters(Font.Characters, Categories, HideWhitespace);
    }

    private PrintViewModel() { }

    public static PrintViewModel Create(FontMapViewModel viewModel)
    {
        PrintViewModel model = new()
        {
            ShowColorGlyphs = viewModel.ShowColorGlyphs,
            Typography = viewModel.SelectedTypography.Feature,
            FontFamily = viewModel.SelectedFaceAnalysis.FontFamily,
            Font = viewModel.SelectedFace,
            Annotation = viewModel.Settings.GlyphAnnotation,
            Categories = viewModel.SelectedGlyphCategories.Select(c => c.Clone()).ToList()
        };

        model.UpdateCharacters();
        return model;
    }
}
