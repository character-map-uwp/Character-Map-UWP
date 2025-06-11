using CharacterMapCX.Controls;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace CharacterMap.Views;

public sealed partial class QuickCompareView : ViewBase, IInAppNotificationPresenter
{
    public QuickCompareViewModel ViewModel { get; }

    public QuickCompareView() : this(new(false)) { }

    private NavigationHelper _navHelper { get; } = new();

    public QuickCompareView(QuickCompareArgs args)
    {
        this.InitializeComponent();

        ViewModel = new QuickCompareViewModel(args);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        this.DataContext = this;

        VisualStateManager.GoToState(this, GridLayoutState.Name, false);

        if (ViewModel.IsQuickCompare)
            VisualStateManager.GoToState(this, QuickCompareState.Name, false);

        if (ViewModel.IsFolderMode)
            VisualStateManager.GoToState(this, FontFolderState.Name, false);

        _navHelper.BackRequested += (s, e) => { ViewModel.SelectedFont = null; };

        if (DesignMode)
            return;

        this.Opacity = 0;

        ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseVisible);
    }

    protected override void OnLoaded(object sender, RoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, NormalState.Name, false);
        TitleBarHelper.SetTitle(Presenter.Title);
        _navHelper.Activate();

        Register<AppNotificationMessage>(OnNotificationMessage);
        Register<CollectionsUpdatedMessage>(HandleMessage);
        Register<CollectionRequestedMessage>(HandleMessage);

        AnimateIn();
    }

    protected override void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel?.Deactivated();
        _navHelper.Deactivate();

        Messenger.UnregisterAll(this);
    }

    private void AnimateIn()
    {
        this.Opacity = 1;
        if (ResourceHelper.AllowAnimation is false)
            return;

        int s = 66;
        int o = 110;

        // Title
        CompositionFactory.PlayEntrance(Presenter.GetTitleElement(), s + 30, o);

        // First Row
        CompositionFactory.PlayEntrance(TopRow, s + 113, o);

        // Second Row
        CompositionFactory.PlayEntrance(SecondRow, s + 200, o);

        // Third Row
        CompositionFactory.PlayEntrance(FontsRoot, s + 300, o);
    }

    private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.FontList))
        {
            if (ViewStates.CurrentState != NormalState)
                GoToNormalState();

            if (ResourceHelper.AllowAnimation)
                CompositionFactory.PlayEntrance(Repeater, 0, 80, 0);

            // ItemsRepeater is a bit rubbish, needs to be nudged back into life.
            // If we scroll straight to zero, we can often end up with a blank screen
            // until the user scrolls. So we need to manually hack in a scroll ourselves.
            //ListingScroller.ChangeView(null, 2, null, true);
            //_ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            //{
            //    await Task.Delay(16);
            //    ListingScroller?.ChangeView(null, 0, null, false);
            //});
        }
        else if (e.PropertyName == nameof(ViewModel.SelectedFont))
        {
            if (ViewModel.SelectedFont is null)
            {
                GoToNormalState();
            }
            else
            {
                DetailsFontTitle.Text = "";
                GoToState(DetailsState.Name);
            }
        }
        else if (e.PropertyName == nameof(ViewModel.Text))
        {
            UpdateText(ViewModel.Text);
        }
    }

    private void GoToNormalState()
    {
        // Repeater metrics may be out of date. Update.
        UpdateText(ViewModel.Text, Repeater.Realize().ItemsPanelRoot);
        UpdateFontSize(FontSizeSlider.Value, Repeater.Realize().ItemsPanelRoot);
        GoToState(NormalState.Name);
    }

    private void Button_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Button b && e.GetCurrentPoint(b).Properties.IsMiddleButtonPressed
            && b.Content is CMFontFamily font)
        {
            _ = FontMapView.CreateNewViewForFontAsync(font);
        }
    }

    private void InputText_DetectedFlowDirectionChanged(object sender, FlowDirection e)
    {
        UpdateText(ViewModel.Text, flowOnly: true);
    }

    bool IsDetailsView => ViewStates.CurrentState == DetailsState;

    /*
     * ElementName Bindings don't work inside ItemsRepeater, so to change
     * preview Text & FontSize we need to manually update all TextBlocks
     */

    void UpdateText(string text, FrameworkElement root = null, bool flowOnly = false)
    {
        FrameworkElement target = root ?? (IsDetailsView ? DetailsRepeater : Repeater.Realize().ItemsPanelRoot);
        if (target == null)
            return;

        XamlBindingHelper.SuspendRendering(target);
        var flow = InputText.DetectedFlowDirection;
        foreach (var g in GetTargets(target))
        {
            if (flowOnly is false)
                SetText(g, text);
            SetFlow(g, flow);
        }
        XamlBindingHelper.ResumeRendering(target);
    }

    void UpdateFontSize(double size, FrameworkElement root = null)
    {
        FrameworkElement target = root ?? (IsDetailsView ? DetailsRepeater : Repeater.Realize().ItemsPanelRoot);
        if (target == null)
            return;

        XamlBindingHelper.SuspendRendering(target);
        foreach (var g in GetTargets(target))
            SetFontSize(g, size);
        XamlBindingHelper.ResumeRendering(target);
    }

    IEnumerable<FrameworkElement> GetTargets(FrameworkElement target)
    {
        if (target.DesiredSize.Height == 0 && target.DesiredSize.Width == 0)
            target.Measure(new Windows.Foundation.Size(50, 50));

        return target.GetFirstLevelDescendants(d => (d is TextBlock or DirectText) && d.Name.EndsWith("Render"));
    }

    private void FontSizeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (Repeater == null)
            return;

        double v = e.NewValue;
        UpdateFontSize(v);
    }

    private void ZoomHelper_ZoomRequested(object sender, double e)
    {
        FontSizeSlider.Value += e;
    }

    void SetText(object t, string text)
    {
        if (t is TextBlock tb)
            tb.Text = text ?? string.Empty;
        else if (t is DirectText d)
            d.Text = text;
    }

    void SetFlow(object t, FlowDirection dir)
    {
        if (t is FrameworkElement f)
            f.FlowDirection = dir;
    }

    void SetFontSize(object t, double size)
    {
        if (t is TextBlock tb)
            tb.FontSize = size;
        else if (t is DirectText d)
            d.FontSize = size;
    }

    private void DetailsRepeater_ElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
    {
        // Unload x:Bind content
        if (args.Element is FrameworkElement f && f.Tag is FrameworkElement t)
        {
            UnloadObject(t);
            f.Tag = null;
        }
    }

    private void Repeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is Button b && b.Content is Panel g)
        {
            SetText(g, InputText.Text);
            SetFlow(g, InputText.DetectedFlowDirection);
            SetFontSize(g, FontSizeSlider.Value);
        }
        else if (args.Element is FrameworkElement p && GetTargets(p).FirstOrDefault() is FrameworkElement t)
        {
            SetText(t, InputText.Text);
            SetFlow(t, InputText.DetectedFlowDirection);
            SetFontSize(t, FontSizeSlider.Value);
        }
    }

    private void ItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Content is CMFontFamily font && Repeater is ListViewBase list)
        {
            if (ResourceHelper.AllowAnimation)
            {
                var item = list.ContainerFromItem(font);
                var title = item.GetFirstDescendantOfType<TextBlock>();
                ConnectedAnimationService.GetForCurrentView().DefaultDuration = TimeSpan.FromSeconds(0.7);
                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("Title", title);
            }

            ViewModel.SelectedFont = font;
        }
        else if (sender is Button bn && bn.Content is CharacterRenderingOptions o && ViewModel.IsQuickCompare)
        {
            ContextFlyout.SetItemsDataContext(o);
            ContextFlyout.ShowAt(bn);
        }
    }

    private void Repeater_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (sender is GridView g && e.ClickedItem is CharacterRenderingOptions o && ViewModel.IsQuickCompare)
        {
            ContextFlyout.SetItemsDataContext(o);
            ContextFlyout.ShowAt(g.ContainerFromItem(e.ClickedItem) as FrameworkElement);
        }
    }

    private void ItemContextRequested(UIElement sender, Windows.UI.Xaml.Input.ContextRequestedEventArgs args)
    {
        if (ViewModel.IsQuickCompare && sender is Button b)
        {
            ContextFlyout.SetItemsDataContext(b.Content);
            ContextFlyout.ShowAt(b);
        }
        else if (sender is Button bu && bu.Content is CMFontFamily font)
        {
            // 1. Clear the context menu
            while (MainContextFlyout.Items.Count > 1)
                MainContextFlyout.Items.Remove(MainContextFlyout.Items[^1]);

            // 2. Rebuild with the correct collection information
            MainContextFlyout.AddSeparator();
            FlyoutHelper.AddCollectionItems(MainContextFlyout, font, null);
            FlyoutHelper.TryAddRemoveFromCollection(
                MainContextFlyout, font, ViewModel.SelectedCollection, ViewModel.FontListFilter);

            // 3. Show Flyout
            MainContextFlyout.SetItemsDataContext(bu.Content);
            if (args.TryGetPosition(bu, out Point p))
                MainContextFlyout.ShowAt(bu, p);
            else
                MainContextFlyout.ShowAt(bu);
        }
    }

    private void OpenWindow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item)
        {
            if (item.DataContext is CharacterRenderingOptions o
                && FontFinder.Fonts.FirstOrDefault(f => f.Variants.Contains(o.Variant)) is CMFontFamily font)
            {
                _ = FontMapView.CreateNewViewForFontAsync(font, null, o);
            }
            else if (item.DataContext is CMFontFamily f)
            {
                _ = FontMapView.CreateNewViewForFontAsync(f);
            }
        }
    }

    private void Remove_Clicked(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.DataContext is CharacterRenderingOptions o)
            ViewModel.QuickFonts.Remove(o);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedFont = null;
    }

    private void RadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is RadioButtons b)
        {
            if (b.SelectedIndex == 0)
                GoToState(GridLayoutState.Name);
            else if (b.SelectedIndex == 1)
                GoToState(StackLayoutState.Name);
        }
    }

    private void ViewStates_CurrentStateChanging(object sender, VisualStateChangedEventArgs e)
    {
        if (e.NewState == DetailsState)
        {
            DetailsFontTitle.Text = ViewModel.SelectedFont.Name;

            if (ResourceHelper.AllowAnimation is false)
                return;

            var ani = ConnectedAnimationService.GetForCurrentView().GetAnimation("Title");
            ani.TryStart(DetailsTitleContainer);//, new List<UIElement> { DetailsViewContent });
        }
    }

    private void Repeater_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Phase == 1)
        {
            var g = GetTargets(args.ItemContainer).FirstOrDefault();
            if (!args.InRecycleQueue && g is not null)
            {
                g.DataContext = args.Item;
                SetText(g, ViewModel.Text);
                SetFlow(g, InputText.DetectedFlowDirection);
                SetFontSize(g, FontSizeSlider.Value);
            }
        }
        else
        {
            // This is hack for Quick Compare view - force ItemTemplate
            // to be inflated so our code will work
            if (args.ItemContainer.Content is null)
            {
                args.ItemContainer.Content = args.Item;
                args.ItemContainer.Measure(new Windows.Foundation.Size(50, 50));
            }

            args.RegisterUpdateCallback(1, Repeater_ContainerContentChanging);

            if (ResourceHelper.AllowExpensiveAnimation && LayoutModeStates.CurrentState == GridLayoutState)
            {
                if (args.InRecycleQueue)
                    CompositionFactory.PokeUIElementZIndex(args.ItemContainer);
                else
                    SetAnimation(args.ItemContainer, true);
            }
            else
                SetAnimation(args.ItemContainer, false);
        }
    }

    private void LayoutModeStates_CurrentStateChanging(object sender, VisualStateChangedEventArgs e)
    {
        if (Repeater.ItemsPanelRoot is null)
            return;

        // Attempting to change this in XAML causes crashes.
        // We don't won't reposition animations in stack layout
        bool enable = e.NewState == GridLayoutState;
        foreach (var item in Repeater.ItemsPanelRoot.Children.OfType<SelectorItem>())
            SetAnimation(item, enable);
    }

    void SetAnimation(SelectorItem item, bool enable)
    {
        var v = ElementCompositionPreview.GetElementVisual(item);
        v.ImplicitAnimations = enable ? CompositionFactory.GetRepositionCollection(v.Compositor) : null;
    }




    /* Notification Helpers */

    private void HandleMessage(CollectionsUpdatedMessage obj)
    {
        if (obj.SourceCollection is not null && obj.SourceCollection == ViewModel.SelectedCollection)
        {
            RunOnUI(() => ViewModel.RefreshFontList(ViewModel.SelectedCollection));
        }
    }

    private void HandleMessage(CollectionRequestedMessage obj)
    {
        if (Dispatcher.HasThreadAccess)
        {
            obj.Handled = true;
            ViewModel.SelectedCollection = obj.Collection;
            GoToNormalState();
        }
    }

    public InAppNotification GetNotifier()
    {
        if (NotificationRoot == null)
            this.FindName(nameof(NotificationRoot));

        return DefaultNotification;
    }

    void OnNotificationMessage(AppNotificationMessage msg)
    {
        if (msg.Data is AddToCollectionResult result
            && result.Success
            && result.Collection is not null
            && result.Collection == ViewModel.SelectedCollection
            && Dispatcher.HasThreadAccess == false)
        {
            // If we don't have thread access, it means another window has added an item to
            // the collection we're currently viewing, and we should refresh our view
            RunOnUI(() => ViewModel.RefreshFontList(ViewModel.SelectedCollection));
        }

        if (!Dispatcher.HasThreadAccess)
            return;

        InAppNotificationHelper.OnMessage(this, msg);
    }
}





public partial class QuickCompareView
{
    public static async Task<WindowInformation> CreateWindowAsync(QuickCompareArgs args)
    {
        // 1. If QuickCompare (rather than FontCompare), return the existing window
        //    if we have one. (QuickCompare is ALWAYS single window)
        if (args.IsQuickCompare && QuickCompareViewModel.QuickCompareWindow is not null)
            return QuickCompareViewModel.QuickCompareWindow;

        static void CreateView(QuickCompareArgs a)
        {
            QuickCompareView view = new(a);
            Window.Current.Content = view;
            Window.Current.Activate();
        }

        var view = await WindowService.CreateViewAsync(() => CreateView(args), false);
        await WindowService.TrySwitchToWindowAsync(view, false);

        if (args.IsQuickCompare)
            QuickCompareViewModel.QuickCompareWindow = view;

        return view;
    }

    public static async Task AddAsync(CharacterRenderingOptions options, bool wholeFamily = false)
    {
        // 1. Ensure QuickCompare Window exists
        var window = await CreateWindowAsync(new(true));

        // 2. Add selected font to QuickCompare
        await QuickCompareViewModel.QuickCompareWindow.CoreView.Dispatcher.ExecuteAsync(() =>
        {
            WeakReferenceMessenger.Default.Send(options, 
                wholeFamily 
                    ? QuickCompareViewModel.MultiToken
                    : nameof(QuickCompareViewModel));
        });

        // 3. Try switch to view.
        //    Task.Delay is required as TrySwitchToWindow may fail.
        await Task.Delay(64);
        await WindowService.TrySwitchToWindowAsync(QuickCompareViewModel.QuickCompareWindow, false);
    }
}
