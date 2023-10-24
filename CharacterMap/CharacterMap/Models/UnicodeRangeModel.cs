namespace CharacterMap.Models;

public partial class UnicodeRangeModel : ObservableObject
{
    public NamedUnicodeRange Range { get; }

    [ObservableProperty] bool _isSelected = true;

    public UnicodeRangeModel(NamedUnicodeRange range)
    {
        Range = range;
    }

    public UnicodeRangeModel Clone() => new (Range) { IsSelected = _isSelected };
}
