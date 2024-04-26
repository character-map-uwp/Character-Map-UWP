using CharacterMapCX.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

[DependencyProperty<CharacterRenderingOptions>("Options")]
public sealed partial class CharacterPicker : Control
{
    public event EventHandler<Character> CharacterSelected;

    private Flyout _parent = null;

    public CharacterPicker()
    {
        this.DefaultStyleKey = typeof(CharacterPicker);
    }

    public CharacterPicker(Flyout parent, CharacterRenderingOptions options) : this()
    {
        _parent = parent;
        Options = options;
    }

    CharacterGridView _itemsGridView = null;

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_itemsGridView is not null)
            _itemsGridView.ItemDoubleTapped -= ItemsGridView_ItemDoubleTapped;

        if (this.GetTemplateChild("ItemsGridView") is CharacterGridView g)
        {
            _itemsGridView = g;
            g.ItemDoubleTapped -= ItemsGridView_ItemDoubleTapped;
            g.ItemDoubleTapped += ItemsGridView_ItemDoubleTapped;
        }

        if (this.GetTemplateChild("AddButton") is Button b)
        {
            b.Click -= Add_Click;
            b.Click += Add_Click;
        }

        if (this.GetTemplateChild("CloseButton") is Button c)
        {
            c.Click -= Close_Click;
            c.Click += Close_Click;
        }
    }
    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (_itemsGridView.SelectedItem is Character c)
            CharacterSelected?.Invoke(this, c);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (_parent is not null)
        {
            _parent.Hide();
            _parent = null;
        }
    }

    private void ItemsGridView_ItemDoubleTapped(object sender, ICharacter e)
    {
        CharacterSelected?.Invoke(this, (Character)e);
    }
}
