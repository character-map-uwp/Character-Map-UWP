namespace CharacterMap.ViewModels;

public partial class GlyphFileNameViewModel : ObservableObject
{
    private static Character _defaultChar { get; } = new(65);
    private static Character _defaultChar2 { get; } = new(63);

    [ObservableProperty] string _template = ExportOptions.DefaultTemplate;
    [ObservableProperty] string _example;
    [ObservableProperty] bool _isExpanded;

    public bool SaveTemplate { get; set; }

    private InstalledFont _lastFont = null;
    private CharacterRenderingOptions _lastOptions = null;

    public void SetOptions(InstalledFont font, CharacterRenderingOptions options)
    {
        _lastFont = font;
        _lastOptions = options;

        UpdateExample();
    }

    public void Reset() => Template = ExportOptions.DefaultTemplate;

    public void ToggleExpansion() => IsExpanded = !IsExpanded;

    partial void OnTemplateChanged(string value)
    {
        UpdateExample();

        if (SaveTemplate)
            ResourceHelper.AppSettings.FileNameTemplate = value;
    }

    void UpdateExample()
    {
        ExportOptions options = new(ExportFormat.Png, ExportStyle.Black)
        {
            Font = _lastFont,
            Options = _lastOptions,
            FileNameTemplate = Template
        };

        Example =  $"{Localization.Get("ExampleFormat", options.GetFileName(_defaultChar, "png"))}\n" +
                   $"{Localization.Get("ExampleFormat", options.GetFileName(_defaultChar2, "png"))}";
    }
}
