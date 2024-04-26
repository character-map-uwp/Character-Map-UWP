using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace CharacterMap.Views;

[ObservableObject]
public abstract partial class ViewBase : Page
{
    protected IMessenger Messenger => WeakReferenceMessenger.Default;

    /// <summary>
    /// Returns true if the process is running within the VS designer
    /// </summary>
    protected bool DesignMode => Windows.ApplicationModel.DesignMode.DesignModeEnabled;

    public ViewBase()
    {
        this.Loaded += OnLoadedBase;
        this.Unloaded += OnUnloaded;

        ResourceHelper.GoToThemeState(this);

        if (DesignMode)
            return;

        LeakTrackingService.Register(this);
    }

    private void OnLoadedBase(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
        ResourceHelper.GoToThemeState(this);
        OnLoaded(sender, e);
    }

    protected virtual void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
    }

    protected virtual void OnUnloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
        Messenger.UnregisterAll(this);
    }

    protected void Register<T>(Action<T> handler) where T : class
    {
        Messenger.Register<T>(this, (o, m) => handler(m));
    }

    protected void Unregister<T>() where T : class
    {
        Messenger.Unregister<T>(this);
    }

    protected void RunOnUI(Action a)
    {
        this.RunOnDispatcher(a);
    }

    /// <summary>
    /// Transitions this Control to the named VisualState, with transitions controlled by <see cref="ResourceHelper.AllowAnimation"/>
    /// </summary>
    /// <param name="state">Name to state to transition too</param>
    /// <param name="tryAnimate">Attempt to animate. Will be ignored if <see cref="ResourceHelper.AllowAnimation"/> returns false</param>
    /// <returns>**true** if the control is now in the named state</returns>
    protected bool GoToState(string state, bool tryAnimate = true)
    {
        try
        {
            return VisualStateManager.GoToState(this, state, tryAnimate && ResourceHelper.AllowAnimation);
        }
        catch
        {
            return false;
        }
    }

    protected TransitionCollection GetRepositionCollection(bool b)
    {
        return b
            ? ResourceHelper.Get<TransitionCollection>("RepositionTransitions")
            : null;
    }

    /// <summary>
    /// Returns a local transition collection
    /// </summary>
    /// <param name="key"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    protected TransitionCollection GetTransitions(string key, bool b)
    {
        return b
            ? ResourceHelper.Get<TransitionCollection>(this, key)
            : ResourceHelper.Get<TransitionCollection>("NoTransitions");
    }
}
