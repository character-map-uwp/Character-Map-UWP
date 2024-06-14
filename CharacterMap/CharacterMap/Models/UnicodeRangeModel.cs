using System.Diagnostics;

namespace CharacterMap.Models;

[DebuggerDisplay("{Range.Name}, Selected: {IsSelected}")]
public partial class UnicodeRangeModel : ObservableObject
{
    public NamedUnicodeRange Range { get; }

    [ObservableProperty] bool _isSelected = true;

    public UnicodeRangeModel(NamedUnicodeRange range)
    {
        Range = range;
    }

    public UnicodeRangeModel Clone() => new(Range) { IsSelected = _isSelected };
}
