using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Views;

public interface IPopoverPresenter
{
    Border GetPresenter();
    FontMapView GetFontMap();
    GridLength GetTitleBarHeight();
}

public partial class PopoverViewBase : ViewBase
{
    [ObservableProperty]
    protected GridLength _titleBarHeight = new(32);

    protected IPopoverPresenter _presenter = null;

    protected FontMapView _fontMap = null;

    protected NavigationHelper _navHelper { get; } = new NavigationHelper();

    public PopoverViewBase()
    {
        _navHelper.BackRequested += (s, e) => Hide();
    }

    public virtual void Show()
    {
        _navHelper.Activate();
    }

    public virtual void Hide()
    {
        if (_presenter == null)
            return;

        _navHelper.Deactivate();

        _presenter.GetPresenter().Child = null;
        _presenter = null;
        _fontMap = null;

        TitleBarHelper.RestoreDefaultTitleBar();
        Messenger.Send(new ModalClosedMessage());
    }
}
