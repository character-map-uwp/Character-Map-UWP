using CharacterMap.Controls;
using CharacterMapCX.Controls;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace CharacterMap.Views;

public sealed partial class QuickCompareView : ViewBase, IInAppNotificationPresenter
{
    public QuickCompareViewModel ViewModel { get; private set; }

    public QuickCompareView() : this(new(false)) { }

    private UWPNavigationHelper _navHelper { get; } = new();

    private ConnectedAnimation _titleAnimation;

    public bool IsStandalone { get; set; } = true;

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

        _navHelper.BackRequested += NavHelper_BackRequested;

        if (DesignMode)
            return;

        this.Opacity = 0;
    }

    protected override void OnLoading()
    {

        if (IsStandalone)
        {
            ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseVisible);
        }

        if (IsStandalone is false && ViewModel.Settings.UseMenuNavigationBar)
        {
            Presenter.Background = null;

            if (ResourceHelper.AllowAnimation)
                CompositionFactory.SetupOverlayPanelAnimation(this, true);
        }
    }

    protected override void OnLoaded(object sender, RoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, NormalState.Name, false);
        _navHelper.Activate();

        if (IsStandalone)
        {
            TitleBarHelper.SetTitle(Presenter.Title);
            Register<AppNotificationMessage>(OnNotificationMessage);
        }
        
        Register<CollectionsUpdatedMessage>(HandleMessage);
        Register<CollectionRequestedMessage>(HandleMessage);

        AnimateIn();
    }

    private void NavHelper_BackRequested(object sender, EventArgs e)
    {
        if (ViewModel != null)
            ViewModel.SelectedFont = null;
    }

    private void OpenFontButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.OpenCurrentFont();
    }

    protected override void OnUnloaded(object sender, RoutedEventArgs e)
    {
        base.OnUnloaded(sender, e);

        // 0. Stop tracking bindings
        this.Bindings.StopTracking();

        // 1. Cleanly transition ViewStates to NormalState without animation to clear DetailsState setters
        //VisualStateManager.GoToState(this, NormalState.Name, false);

        // 2. Unhook view & control events
        //ViewStates.CurrentStateChanging -= ViewStates_CurrentStateChanging;
        //BackButton.Click -= BackButton_Click;
        //OpenFontButton.Click -= OpenFontButton_Click;
        //FontSizeSlider.ValueChanged -= FontSizeSlider_ValueChanged;
        //InputText.DetectedFlowDirectionChanged -= InputText_DetectedFlowDirectionChanged;

        //// 3. Unhook repeater events
        //Repeater.ContainerContentChanging -= Repeater_ContainerContentChanging;
        //Repeater.ItemClick -= Repeater_ItemClick;
        //Repeater.EnableRepositionAnimations = false;

        //DetailsRepeater.ContainerContentChanging -= Repeater_ContainerContentChanging;
        //DetailsRepeater.EnableRepositionAnimations = false;

        //// 4. Cancel connected animations
        //if (_titleAnimation is not null)
        //{
        //    _titleAnimation.Cancel();
        //    _titleAnimation = null;
        //}
        //ConnectedAnimationService.GetForCurrentView().GetAnimation("Title")?.Cancel();

        //// 5. Detach ZoomHelper
        if (Properties.GetZoomHelper(FontsRoot) is { } zh)
        {
            zh.ZoomRequested -= ZoomHelper_ZoomRequested;
            zh.Detach(FontsRoot);
            Properties.SetZoomHelper(FontsRoot, null);
        }

        // 6. Destroy flyouts
        if (FontListFilter.Flyout is FilterFlyout menu)
            menu.Destroy();
        FontListFilter.Flyout = null;

        // 8. Clear items
        DetailsRepeater.TearDown();
        Repeater.TearDown();

        // 9. Navigation helper
        _navHelper.BackRequested -= NavHelper_BackRequested;
        _navHelper.Deactivate();

        // 10. Title bar cleanup
        if (IsStandalone is false)
            TitleBarHelper.RestoreDefaultTitleBar();

        // 11. Teardown ViewModel
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            //ViewModel.SelectedFont = null;
            ViewModel.Deactivated();
            ViewModel = null;
        }

        DataContext = null;
    }

    private void AnimateIn()
    {
        this.Opacity = 1;
        if (ResourceHelper.AllowAnimation is false)
            return;

        int s = 66;
        int o = 110;

        // Title
        CompositionFactory.PlayEntrance(Presenter.GetTitleContainerElement(), s + 30, o);

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
                GoToNormalState();
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

    Windows.Foundation.Size GetTargetSurfaceSize(double fontSize)
    {
        if (LayoutModeStates.CurrentState == StackLayoutState)
            return new(0, 0);

        double itemWidth = Repeater?.ItemsPanelRoot is ItemsWrapGrid wrap && wrap.ItemWidth > 0
            ? wrap.ItemWidth
            : 600;
        return new(Math.Max(500, itemWidth), 226);
    }

    void UpdateText(string text, FrameworkElement root = null, bool flowOnly = false)
    {
        FrameworkElement target = root ?? (IsDetailsView ? DetailsRepeater.ItemsPanelRoot : Repeater.ItemsPanelRoot);
        if (target == null)
            return;

        XamlBindingHelper.SuspendRendering(target);
        FlowDirection flow = InputText.DetectedFlowDirection;
        foreach (FrameworkElement g in GetTargets(target))
        {
            if (g is DirectText d)
            {
                d.BlockUpdates = true;
                if (flowOnly is false)
                    SetText(d, text);
                SetFlow(d, flow);
                d.Update();
            }
            else
            {
                if (flowOnly is false)
                    SetText(g, text);
                SetFlow(g, flow);
            }
        }
        XamlBindingHelper.ResumeRendering(target);
    }

    void UpdateFontSize(double size, FrameworkElement root = null)
    {
        FrameworkElement target = root ?? (IsDetailsView ? DetailsRepeater.ItemsPanelRoot : Repeater.ItemsPanelRoot);
        if (target == null)
            return;

        XamlBindingHelper.SuspendRendering(target);
        Windows.Foundation.Size surfaceSize = GetTargetSurfaceSize(size);
        foreach (FrameworkElement g in GetTargets(target))
        {
            if (g is DirectText d)
            {
                d.BlockUpdates = true;
                d.FontSize = size;
                d.SurfaceSize = surfaceSize;
                d.Update();
            }
            else
                SetFontSize(g, size);
        }
        XamlBindingHelper.ResumeRendering(target);
    }

    IEnumerable<FrameworkElement> GetTargets(FrameworkElement target)
    {
        if (target.DesiredSize.Height == 0 && target.DesiredSize.Width == 0)
            target.Measure(new Windows.Foundation.Size(50, 50));

        if (target is ListViewBase b)
            return b.GetActiveContainers().Select(c => c.GetFirstDescendantOfType<DirectText>());

        return target.GetFirstLevelDescendantsOfType<DirectText>();// (d => (d is TextBlock or DirectText) && d.Name.EndsWith("Render"));
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
            d.SetValue(DirectText.TextProperty, text ?? DependencyProperty.UnsetValue);
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
        if (args.Element is SelectorItem b && b.Content is Panel g)
        {
            SetText(g, InputText.Text);
            SetFlow(g, InputText.DetectedFlowDirection);
            SetFontSize(g, FontSizeSlider.Value);
        }
        else if (args.Element is FrameworkElement p && GetTargets(p).FirstOrDefault() is DirectText d)
        {
            //if (t is DirectText d)
            {
                d.BlockUpdates = true;
                SetText(d, InputText.Text);
                SetFlow(d, InputText.DetectedFlowDirection);
                d.FontSize = FontSizeSlider.Value;
                d.SurfaceSize = GetTargetSurfaceSize(FontSizeSlider.Value);
                d.Update();
            }
            //else
            //{
            //    SetText(t, InputText.Text);
            //    SetFlow(t, InputText.DetectedFlowDirection);
            //    SetFontSize(t, FontSizeSlider.Value);
            //}
        }
    }

    private void Repeater_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue)
        {
            if (args.ItemContainer.ContentTemplateRoot is ContentGroup cg && cg.SecondaryContent is DirectText d)
                d.CancelRender();
            else if (GetTargets(args.ItemContainer).FirstOrDefault() is DirectText dt)
                dt.CancelRender();

            args.Handled = false;
            return;
        }

        args.Handled = true;
        object item = args.Item is CMFontFamily family ? family.DefaultFace : args.Item;

        if (args.Phase == 1)
        {
            if (args.ItemContainer.Content != item)
            {
                args.Handled = false;
                return;
            }

            if (args.Item is CMFontFamily f)
                Update(args.ItemContainer.ContentTemplateRoot as ContentGroup, f, f.DefaultFace, args.Phase, item);
            else if (args.Item is CMFontFace fc)
                Update(args.ItemContainer.ContentTemplateRoot as ContentGroup, null, fc, args.Phase, item);
            else if (GetTargets(args.ItemContainer).FirstOrDefault() is DirectText d)
            {
                //if (g is DirectText d)
                {
                    d.BlockUpdates = true;
                    d.DataContext = item;
                    SetText(d, ViewModel.Text);
                    SetFlow(d, InputText.DetectedFlowDirection);
                    d.FontSize = FontSizeSlider.Value;
                    d.SurfaceSize = GetTargetSurfaceSize(FontSizeSlider.Value);
                    d.Update();
                }
                //else
                //{
                //    g.DataContext = item;
                //    SetText(g, ViewModel.Text);
                //    SetFlow(g, InputText.DetectedFlowDirection);
                //    SetFontSize(g, FontSizeSlider.Value);
                //}
            }

            return;
        }

        // Phase 0: realize container template & update header text
        args.ItemContainer.Content = item;
        args.ItemContainer.Realize();
        if (args.ItemContainer.ContentTemplateRoot is FrameworkElement root)
            root.DataContext = item;

        if (args.Item is CMFontFamily fam)
            Update(args.ItemContainer.ContentTemplateRoot as ContentGroup, fam, fam.DefaultFace, args.Phase, item);
        else if (args.Item is CMFontFace fce)
            Update(args.ItemContainer.ContentTemplateRoot as ContentGroup, null, fce, args.Phase, item);

        args.RegisterUpdateCallback(1, Repeater_ContainerContentChanging);
    }

    void Update(ContentGroup container, CMFontFamily family, CMFontFace face, uint phase = 0, object item = null)
    {
        // bool native = true;// face.SupportsColourRendering;
        //string name = native ? "NativeRender" : "SystemRender";
        if (container?.SecondaryContent is not DirectText d)
            return;

        //FrameworkElement existing = g.Children.FirstOrDefault() as FrameworkElement;
        if (phase == 0)
        {
            // 2. Update font name
            if (container.Text is TextBlock t)
            {
                if (t.Inlines.FirstOrDefault() is Run r)
                    r.Text = family?.DefaultFace.FamilyName ?? face.FamilyName;

                if (t.Inlines.Skip(2).FirstOrDefault() is Run r1)
                    r1.Text = (family != null ? Localization.Get("FaceCountLabel", family.RealFaces.Count) : face.PreferredName);
            }

            return;
        }

        // 3. PHASE 1: Realize target element if needed & apply all properties in one batch
        //existing ??= g.FindName(name) as FrameworkElement;

        //if (existing is null)
        //    Debugger.Break();

        XamlBindingHelper.SuspendRendering(d);

        d.BlockUpdates = true;
        d.DataContext = item ?? face;
        d.SetValue(DirectText.TextProperty, ViewModel.Text ?? DependencyProperty.UnsetValue);
        d.FlowDirection = InputText.DetectedFlowDirection;
        d.FontSize = FontSizeSlider.Value;
        d.SurfaceSize = GetTargetSurfaceSize(FontSizeSlider.Value);
        d.FontFace = face.Face;
        d.FontStretch = face.DirectWriteProperties.Stretch;
        d.FontStyle = face.DirectWriteProperties.Style;
        d.FontWeight = face.DirectWriteProperties.Weight;
        d.Update();

        XamlBindingHelper.ResumeRendering(d);
    }

    private void Repeater_ItemClick(object sender, ItemClickEventArgs e)
    {
        var item = e.ClickedItem is CMFontFamily efamily ? efamily.DefaultFace : e.ClickedItem;

        if (e.ClickedItem is CMFontFamily { DefaultFace: CMFontFace face } family && sender is ListViewBase list)
        {
            ViewModel.SelectedFont = family;
        }

        else if (sender is ListViewBase g && item is CharacterRenderingOptions o && ViewModel.IsQuickCompare)
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
            while (MainContextFlyout.Items.Count > 2)
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
                && FontFinder.Fonts.FirstOrDefault(f => f.RealFaces.Contains(o.Face)) is CMFontFamily font)
            {
                _ = FontMapView.CreateNewViewForFontAsync(font, null, o);
            }
            else if (item.DataContext is CMFontFamily f)
            {
                _ = FontMapView.CreateNewViewForFontAsync(f);
            }
        }
    }

    private void AddFamilyToQC_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item)
        {
            if (item.DataContext is CharacterRenderingOptions o
                && FontFinder.Fonts.FirstOrDefault(f => f.RealFaces.Contains(o.Face)) is CMFontFamily font)
            {
                _ = QuickCompareView.AddAsync(o, true);
            }
            else if (item.DataContext is CMFontFamily f)
            {
                _ = QuickCompareView.AddAsync(
                        CharacterRenderingOptions.CreateDefault(f), true);
            }
        }
    }

    private void AddToQC_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item)
        {
            if (item.DataContext is CharacterRenderingOptions o
                && FontFinder.Fonts.FirstOrDefault(f => f.RealFaces.Contains(o.Face)) is CMFontFamily font)
            {
                _ = QuickCompareView.AddAsync(o);
            }
            else if (item.DataContext is CMFontFace face)
            {
                _ = QuickCompareView.AddAsync(
                        CharacterRenderingOptions.CreateDefault(face, ViewModel.SelectedFont));
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
        if (sender is UXRadioButtons b)
        {
            if (b.SelectedIndex == 0)
                GoToState(nameof(GridLayoutState));
            else if (b.SelectedIndex == 1)
                GoToState(nameof(StackLayoutState));
        }
    }

    private void ViewStates_CurrentStateChanging(object sender, VisualStateChangedEventArgs e)
    {
        if (e.NewState == DetailsState)
        {
            DetailsFontTitle.Text = ViewModel.SelectedFont.Name;

            if (ResourceHelper.AllowAnimation is false)
                return;

            //var ani = ConnectedAnimationService.GetForCurrentView().GetAnimation("Title");
            //ani.TryStart(DetailsTitleContainer);//, new List<UIElement> { DetailsViewContent });
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
        if (HasThreadAccess)
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
            && HasThreadAccess == false)
        {
            // If we don't have thread access, it means another window has added an item to
            // the collection we're currently viewing, and we should refresh our view
            RunOnUI(() => ViewModel.RefreshFontList(ViewModel.SelectedCollection));
        }

        if (!HasThreadAccess)
            return;

        InAppNotificationHelper.OnMessage(this, msg);
    }
}





public partial class QuickCompareView
{
    public static async Task<IViewInformation> CreateWindowAsync(QuickCompareArgs args)
    {
        // 1. If QuickCompare (rather than FontCompare), return the existing window
        //    if we have one. (QuickCompare is ALWAYS single window)
        if (args.IsQuickCompare && QuickCompareViewModel.QuickCompareWindow is not null)
            return QuickCompareViewModel.QuickCompareWindow;

        var view = await ViewService.Service.CreateViewAsync(() => new QuickCompareView(args), false);

        if (args.IsQuickCompare)
            QuickCompareViewModel.QuickCompareWindow = view;

        return view;
    }

    public static async Task AddAsync(CharacterRenderingOptions options, bool wholeFamily = false)
    {
        // 1. Ensure QuickCompare Window exists
        var window = await CreateWindowAsync(new(true));

        // 2. Add selected font to QuickCompare
        await QuickCompareViewModel.QuickCompareWindow.RunOnAsync(() =>
        {
            WeakReferenceMessenger.Default.Send(options, 
                wholeFamily 
                    ? QuickCompareViewModel.MultiToken
                    : nameof(QuickCompareViewModel));
        });

        // 3. Try switch to view.
        //    Task.Delay is required as TrySwitchToWindow may fail.
        await Task.Delay(64);
        await ViewService.Service.TrySwitchToViewAsync(QuickCompareViewModel.QuickCompareWindow, false);
    }
}
