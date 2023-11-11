using Windows.Data.Text;

namespace CharacterMap.ViewModels;

public class UnicodeCategoryModel : ViewModelBase
{
    public UnicodeGeneralCategory Category { get; }

    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    public string DisplayName { get; }

    public UnicodeCategoryModel(UnicodeGeneralCategory category)
    {
        Category = category;
        DisplayName = category.Humanise();
    }
}
