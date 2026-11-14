using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;

[DependencyProperty<bool>("IsActualLoaded")]
public abstract partial class ControlBase : Control
{
    int _loadCount = 0;

    public ControlBase()
    {
        this.Loaded += ControlBase_Loaded;
        this.Unloaded += ControlBase_Unloaded;
    }

    private void ControlBase_Loaded(object sender, RoutedEventArgs e)
    {
        _loadCount++;

        if (VisualTreeHelper.GetParent(this) is null)
            _loadCount = 0;

        if (_loadCount == 1)
        {
            IsActualLoaded = true;
            OnLoaded();
        }
    }

    private void ControlBase_Unloaded(object sender, RoutedEventArgs e)
    {
        _loadCount--;
        if (_loadCount < 0)
            _loadCount = 0;

        if (_loadCount == 0)
        {
            IsActualLoaded = false;
            OnUnloaded();
        }
    }

    protected virtual void OnLoaded() { }

    protected virtual void OnUnloaded() { }
}