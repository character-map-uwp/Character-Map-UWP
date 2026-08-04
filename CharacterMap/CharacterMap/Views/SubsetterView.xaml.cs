using Microsoft.Toolkit.Uwp.UI.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Views;

public sealed partial class SubsetterView : ViewBase, IInAppNotificationPresenter
{
    public SubsetterViewModel ViewModel { get; }

    public SubsetterView() : this(new()) { }

    private NavigationHelper _navHelper { get; } = new();

    private Debouncer _collectionDebouncer {  get; } = new();

    public SubsetterView(SubsetterArgs args)
    {
        ViewModel = new SubsetterViewModel(args);
        TrackState(nameof(ViewModel.ViewState));

        this.DataContext = this;
        this.InitializeComponent();

        _navHelper.BackRequested += (s, e) =>
        {
            ViewModel?.GoBack();
        };
    }

    protected override void OnLoaded(object sender, RoutedEventArgs e)
    {
        _navHelper.Activate();

        VisualStateManager.GoToState(this, "NormalState", false);
        VisualStateManager.GoToState(this, "OverlayState", false);

        TitleBarHelper.SetTitle(Presenter.Title);

        Register<AppNotificationMessage>(OnNotificationMessage);

        ViewModel.StrongMessenger.Register<CollectionChangedMessage>(this, (_,_) =>
        {
            _collectionDebouncer.Debounce(66, ReEvaluate);
        });

        // Pre-create element visuals to ensure animations run
        // properly when requested later
        PresentationRoot.GetElementVisual();

        AnimateIn();
    }

    protected override void OnUnloaded(object sender, RoutedEventArgs e)
    {
        base.OnUnloaded(sender, e);
        ViewModel.StrongMessenger.UnregisterAll(this);

        _navHelper.Deactivate();
    }

    private void NameTextBox_PreviewKeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        /* 
         * If we press TAB whilst focused on a name input TextBox in Preview mode, 
         * focus the next TextBox in the list 
         */
        if (e.Key is not Windows.System.VirtualKey.Tab
            || sender is not FrameworkElement f
            || f.GetFirstAncestorOfType<SelectorItem>() is not SelectorItem container
            || container.GetFirstAncestorOfType<GridView>() is not GridView list
            || (list.Items.IndexOf(container.Content) is int index && index < 0)
            || index + 1 >= list.Items.Count
            || list.ContainerFromIndex(index + 1) is not SelectorItem nextContainer
            || nextContainer.GetFirstDescendantOfType<TextBox>() is not TextBox nextInput)
            return;

        e.Handled = true;
        nextInput.Focus(FocusState.Keyboard);
    }




    /* Warning Icon Helpers */

    private void Warning_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (args.NewValue is FontGlyph g)
            Evaluate(sender, g);
    }

    void ReEvaluate()
    {
        if (ViewModel.ViewState != SubsetterViewModel.PREVIEW_STATE
            || PreviewList.ItemsPanelRoot is not Panel panel)
            return;

        foreach (var b in panel.GetFirstLevelDescendantsOfType<Border>().Where(b => b.Name == "Warning"))
        {
            if (b.DataContext is FontGlyph g)
                Evaluate(b, g);
        }
    }

    void Evaluate(FrameworkElement warning, FontGlyph g)
    {
        warning.SetVisible(ViewModel.ClashingIndexes.Contains(g.Character.UnicodeIndex));
    }




    /* Notification Helpers */

    public InAppNotification GetNotifier()
    {
        if (NotificationRoot == null)
            this.FindName(nameof(NotificationRoot));

        return DefaultNotification;
    }

    void OnNotificationMessage(AppNotificationMessage msg)
    {
        if (!Dispatcher.HasThreadAccess)
            return;

        InAppNotificationHelper.OnMessage(this, msg);
    }




    /* ANIMATION HELPERS */

    #region Animation

    /// <summary>
    /// Animates the page in on first load
    /// </summary>
    private void AnimateIn()
    {
        ContentRoot.Opacity = 1;
        if (ResourceHelper.AllowAnimation is false)
            return;

        int s = 100;
        int o = 110;

        // Title
        CompositionFactory.PlayEntrance(Presenter.GetTitleContainerElement(), s + 30, o);

        // First Row
        CompositionFactory.PlayEntrance(ButtonsPanel, s + 113, o);
        //CompositionFactory.PlayEntrance(InputContainer, s + 113, o);
        //CompositionFactory.PlayEntrance(SliderContainer, s + 113, o);

        // Second Row
        CompositionFactory.PlayEntrance(ContentGrid, s + 200, o);

        // Third Row
        CompositionFactory.PlayEntrance(PresentationRoot, s + 300, o);
    }


    //ConnectedAnimation _addHistoryAnim;


    #endregion



}





public partial class SubsetterView
{
    public static void CreateDefault()
    {
        _ = CreateWindowAsync(new());
    }

    public static async Task<WindowInformation> CreateWindowAsync(SubsetterArgs args)
    {
        static void CreateView(SubsetterArgs a)
        {
            SubsetterView view = new(a);
            Window.Current.Content = view;
            Window.Current.Activate();
        }

        var view = await WindowService.CreateViewAsync(() => CreateView(args), false);
        await WindowService.TrySwitchToWindowAsync(view, false);
        return view;
    }
}
