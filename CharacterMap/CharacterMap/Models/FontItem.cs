using System.ComponentModel;
using Windows.ApplicationModel;

namespace CharacterMap.ViewModels;

/// <summary>
/// A wrapper used to allow us to change which font is open in a tab
/// </summary>
public partial class FontItem : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Tooltip))]
    private string _subTitle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Tooltip))]
    private InstalledFont _font;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTypeRamp))]
    [NotifyPropertyChangedFor(nameof(IsGlyphMap))]
    private FontDisplayMode _displayMode = FontDisplayMode.CharacterMap;

    [ObservableProperty]
    private bool _isCompact;

    public string Tooltip => $"{Font.Name} {_subTitle}";

    public bool IsTypeRamp => DisplayMode == FontDisplayMode.TypeRamp;

    public bool IsGlyphMap => DisplayMode == FontDisplayMode.GlyphMap;

    private FontVariant _selected;
    public FontVariant Selected
    {
        get => _selected;
        set
        {
            if (_selected != value && value is not null)
            {
                _selected = value;
                OnPropertyChanged();
            }
        }
    }

    public FontItem(InstalledFont font)
    {
        _font = font;
        _selected = font.DefaultVariant;
    }

    /// <summary>
    /// Only for use by VS designer
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public FontItem()
    {
        if (DesignMode.DesignModeEnabled is false)
            throw new InvalidOperationException("Constructor only for use by designer");
    }

    public void SetFont(InstalledFont font)
    {
        if (font != Font && font is not null)
        {
            Font = font;
            Selected = font.DefaultVariant;

            // This is an assumption but works for the current UI flow.
            IsCompact = false;
        }
    }

    public void NotifyFontChange()
    {
        OnPropertyChanged(nameof(Font));
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(Selected))
        {
            if (Selected != Font.DefaultVariant)
                SubTitle = Selected.PreferredName;
            else
                SubTitle = "";
        }
    }
}