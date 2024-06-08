using CharacterMap.Controls;
using CharacterMapCX.Controls;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Core.Direct;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace CharacterMap.Core;

/// <summary>
/// XAML Attached Properties
/// </summary>
[Bindable]
[AttachedProperty<BasicFontFilter>("Filter")]  // Used to apply a filter to MenuFlyoutItem's on the filter list */
[AttachedProperty<TypographyFeatureInfo>("Typography")] // Helper to apply TypographyFeatureInfo to a TextBlock */
[AttachedProperty<CharacterRenderingOptions>("Options")] // Applies CharacterRenderingOptions to a DirectText control
[AttachedProperty<bool>("ClipToBounds")]
[AttachedProperty<InsetClip>("InsetClip", "new Thickness(0d)")]
[AttachedProperty<bool>("IsMouseInputEnabled")] // Enables Mouse & Touch input on an InkCanvas
[AttachedProperty<InkToolbarToolButton>("DefaultTool")] // Sets the default tool for an InkToolbar
[AttachedProperty<string>("Name")]
[AttachedProperty<string>("Uppercase", "string.Empty")]
[AttachedProperty<object>("Footer")]
[AttachedProperty<string>("StyleKey")]
[AttachedProperty<string>("IconString")]
[AttachedProperty<IconElement>("Icon")]
[AttachedProperty<DevOption>]
[AttachedProperty<FlyoutBase>("TargetContextFlyout")]  
[AttachedProperty<TransitionCollection>("ItemContainerTransitions")]  
[AttachedProperty<TransitionCollection>("ChildrenTransitions")]  
[AttachedProperty<bool>("UseAttachedStates")] // Allows an element to run Storyboards from it's own resources on VisualState events
[AttachedProperty<bool>("SetContainerBackgroundTransition")] // Helper property for ItemContainerBackgroundTransition
[AttachedProperty<BrushTransition>("ItemContainerBackgroundTransition")] // When attached to a ListView, automatically sets ItemContainer BackgroundTransitions
[AttachedProperty<FrameworkElement>("PopupRoot")]
[AttachedProperty<bool>("SupportAnimatedIcon")] // Enables automated AnimatedIcon support
[AttachedProperty<bool>("UseStandardReposition")] // Enables automatic layout translation animations
[AttachedProperty<double>("ClickAnimationOffset", -2d)] // Enables pointer animations
[AttachedProperty<string>("ClickAnimation")] // Enables pointer animations
[AttachedProperty<double>("PointerAnimationOffset", -2d)] // Enables pointer animations
[AttachedProperty<string>("PointerOverAnimation")] // Enables pointer animations
[AttachedProperty<string>("PointerPressedAnimation")] // Enables pointer animations
[AttachedProperty<bool>("UseExpandContractAnimation")] // Enables Expand/Contract animation on Flyout controls
[AttachedProperty<FlyoutBase>("Flyout")] // Attaches a Flyout on any ButtonBase that opens on click
[AttachedProperty<bool>("IsCompact")] // Sets a TabViewItem to compact state
[AttachedProperty<bool>("IsTabOpenAnimationEnabled")] // Sets a tab open animation on a TabView
[AttachedProperty<bool>("DisableTranslation")] // Fixes flyout animation error
[AttachedProperty<bool>("RequireOpenTab")] // Sets a require open tab on a TabView
[AttachedProperty<double>("RenderScale", 1d)] // Sets CompositeTransform ScaleX/Y
[AttachedProperty<double>("Rotation", 0d)] // Sets RotationAngleInDegrees on elements handout visual
[AttachedProperty<KeyTime>("RotationTransition", "KeyTime.FromTimeSpan(TimeSpan.Zero)")] // Sets transition duration for changes to elements handout visual RotationAngleInDegrees property
[AttachedProperty<string>("Text")] // Sets the text on a RichEditBox
[AttachedProperty<FontStretch>("FontStretch", FontStretch.Normal)] // Sets the FontStretch on a RichEditBox
[AttachedProperty<FontStyle>("FontStyle", FontStyle.Normal)] // Sets the FontStyle on a RichEditBox
[AttachedProperty<FontWeight>("FontWeight", "FontWeights.Normal")] // Sets the FontWeight on a RichEditBox
[AttachedProperty<FontFamily>("FontFamily")] // Sets the FontFamily on a RichEditBox
[AttachedProperty<string>("ToolTipMemberPath")] // PropertyPath on an ItemContainer's Content to use as the ItemContainer's ToolTip
[AttachedProperty<DataTemplate>("ToolTipTemplate")] // ToolTip DataTemplate for ItemsControl
[AttachedProperty<PlacementMode>("ToolTipPlacement", PlacementMode.Mouse)]
[AttachedProperty<object>("ToolTip")] // Sets ToolTip with default Theme style
[AttachedProperty<string>("ToolTipStyleKey")]
[AttachedProperty<string>("GridDefinitions", "string.Empty")]
[AttachedProperty<string>("Hyperlink")]
[AttachedProperty<object>("Tag")]
[AttachedProperty<CoreCursorType>("Cursor", CoreCursorType.Arrow)]
public partial class Properties : DependencyObject
{
    #region FILTER 

    static partial void OnFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MenuFlyoutItem item && e.NewValue is BasicFontFilter f)
        {
            item.CommandParameter = f;
            item.Text = f.DisplayTitle;
        }
    }

    #endregion

    #region TYPOGRAPHY

    static partial void OnTypographyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock t)
        {
            TypographyFeatureInfo i = e.NewValue as TypographyFeatureInfo;
            var x = XamlDirect.GetDefault();
            IXamlDirectObject p = x.GetXamlDirectObject(t);
            CharacterGridView.UpdateTypography(x, p, i);
        }
    }

    #endregion

    #region CLIP TO BOUNDS

    static partial void OnClipToBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement f)
        {
            var v = f.GetElementVisual();
            if (e.NewValue is true)
                v.Clip = v.Compositor.CreateInsetClip();
            else
                v.Clip = null;
        }
    }

    #endregion

    #region InsetClip

    static partial void OnInsetClipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement f && e.NewValue is Thickness t)
        {
            Visual v = f.GetElementVisual();
            if (t.Left > 0 || t.Right > 0 || t.Top > 0 || t.Bottom > 0)
                v.Clip = v.Compositor.CreateInsetClip((float)t.Left, (float)t.Top, (float)t.Right, (float)t.Bottom);
            else
                v.Clip = null;
        }
    }

    #endregion

    #region IsMouseInputEnabled

    static partial void OnIsMouseInputEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is InkCanvas c)
        {
            if (e.NewValue is bool b && b)
                c.InkPresenter.InputDeviceTypes =
                    CoreInputDeviceTypes.Mouse |
                    CoreInputDeviceTypes.Touch |
                    CoreInputDeviceTypes.Pen;
            else
                c.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Pen;
        }
    }

    #endregion

    #region DirectText Options

    static partial void OnOptionsChanged(DependencyObject s, DependencyPropertyChangedEventArgs e)
    {
        if (s is DirectText d)
        {
            if (e.NewValue is CharacterRenderingOptions o)
            {
                d.Axis = o.Axis;
                d.FallbackFont = Converters.GetFontFallback();
                d.FontFace = o.Variant.Face;
                d.FontFamily = (FontFamily)XamlBindingHelper.ConvertValue(typeof(FontFamily), o.Variant.Source);
                d.FontStretch = o.Variant.DirectWriteProperties.Stretch;
                d.FontStyle = o.Variant.DirectWriteProperties.Style;
                d.FontWeight = o.Variant.DirectWriteProperties.Weight;
                d.IsColorFontEnabled = o.IsColourFontEnabled;
                d.Typography = o.DXTypography;
            }
            else
            {
                d.ClearValue(DirectText.AxisProperty);
                d.ClearValue(DirectText.FallbackFontProperty);
                d.ClearValue(DirectText.FontFaceProperty);
                d.ClearValue(DirectText.FontFamilyProperty);
                d.ClearValue(DirectText.FontStretchProperty);
                d.ClearValue(DirectText.FontStyleProperty);
                d.ClearValue(DirectText.FontWeightProperty);
                d.ClearValue(DirectText.IsColorFontEnabledProperty);
                d.ClearValue(DirectText.TypographyProperty);
            }
        }
        else if (s is TextBlock t)
        {
            if (e.NewValue is CharacterRenderingOptions o)
            {
                t.FontFamily = (FontFamily)XamlBindingHelper.ConvertValue(typeof(FontFamily), o.Variant.DisplaySource);
                t.FontStretch = o.Variant.DirectWriteProperties.Stretch;
                t.FontStyle = o.Variant.DirectWriteProperties.Style;
                t.FontWeight = o.Variant.DirectWriteProperties.Weight;
                t.IsColorFontEnabled = o.IsColourFontEnabled;
                SetTypography(t, o.DefaultTypography);
            }
            else
            {
                t.ClearValue(Properties.TypographyProperty);
                t.ClearValue(TextBlock.FontFamilyProperty);
                t.ClearValue(TextBlock.FontStretchProperty);
                t.ClearValue(TextBlock.FontStyleProperty);
                t.ClearValue(TextBlock.FontWeightProperty);
                t.ClearValue(TextBlock.IsColorFontEnabledProperty);
            }
        }
    }

    #endregion

    #region DEFAULT TOOL

    static partial void OnDefaultToolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is InkToolbar t && e.NewValue is InkToolbarToolButton b)
            t.ActiveTool = b;
    }
    
    #endregion

    #region TabViewItem IsCompact

    static partial void OnIsCompactChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TabViewItem item && e.NewValue is bool b)
        {
            VisualStateManager.GoToState(item, b ? "CollapsedTabState" : "FullTabState", ResourceHelper.AllowAnimation);

            if (b)
                item.MaxWidth = 60;
            else
                item.ClearValue(TabViewItem.MaxWidthProperty);
        }
    }

    #endregion

    #region IsTabOpenAnimationEnabled

    static partial void OnIsTabOpenAnimationEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TabView tabs && e.NewValue is bool b)
        {
            // We can only configure animation if the TabView template is loaded.
            // Check that it is, or wait until it is.
            if (tabs.GetFirstDescendantOfType<TabViewListView>() is ListView list)
                ConfigureAnimations(tabs, b);
            else
            {
                tabs.Loaded -= Tabs_Loaded;
                tabs.Loaded += Tabs_Loaded;
            }

            static void Tabs_Loaded(object sender, RoutedEventArgs e)
            {
                if (sender is TabView t)
                    ConfigureAnimations(t, Properties.GetIsTabOpenAnimationEnabled(t));
            }


            static void ConfigureAnimations(TabView view, bool enabled)
            {
                view.Loaded -= Tabs_Loaded;

                List<WeakReference> _tooAdd = new();

                if (view.GetFirstDescendantOfType<TabViewListView>() is ListView list)
                {
                    list.Items.VectorChanged -= Items_VectorChanged;
                    list.ContainerContentChanging -= List_ContainerContentChanging;

                    if (enabled)
                    {
                        list.Items.VectorChanged += Items_VectorChanged;
                        list.ContainerContentChanging += List_ContainerContentChanging;
                    }

                    void Items_VectorChanged(IObservableVector<object> sender, IVectorChangedEventArgs eargs)
                    {
                        if (ResourceHelper.AllowAnimation is false)
                            return;

                        // TabView sometimes decides to give new item containers to existing items already 
                        // in view, and we want to make sure to only animate items that a re new, so make
                        // a record of them.
                        if (eargs.CollectionChange == CollectionChange.ItemInserted)
                            _tooAdd.Add(new WeakReference(sender[(int)eargs.Index]));
                    }

                    void List_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
                    {
                        // Manually trigger the animation every time the tab content changes
                        if (args.Item is not null
                            && args.ItemContainer is not null
                            && ResourceHelper.AllowAnimation
                            && _tooAdd.FirstOrDefault(r => r.IsAlive && r.Target == args.Item) is WeakReference itemRef)
                        {
                            _tooAdd.Remove(itemRef);

                            // We animate the first child because if we animate the TabItem itself hit-testing
                            // will break.
                            var child = args.ItemContainer.GetFirstDescendantOfType<FrameworkElement>();
                            Visual v = child.EnableCompositionTranslation().GetElementVisual();

                            // Get the animation from cache
                            v.StartAnimation(v.GetCached("_tbopai", () =>
                            {
                                return v.CreateVector3KeyFrameAnimation(CompositionFactory.TRANSLATION)
                                            .SetDelayBehavior(AnimationDelayBehavior.SetInitialValueBeforeDelay)
                                            .SetDelayTime(0.05)
                                            .AddKeyFrame(0, 0, 60)
                                            .AddKeyFrame(1, 0, 0)
                                            .SetDuration(0.325);
                            }));
                        }

                        // Clean up WeakReferences
                        foreach (var item in _tooAdd.ToList())
                            if (item.IsAlive is false)
                                _tooAdd.Remove(item);

                        // Quick hack to apply GetRequireOpenTab states if only one tab is open by default
                        if (args.ItemContainer is not null
                            && sender.Items.Count == 1
                            && GetRequireOpenTab(view))
                            VisualStateManager.GoToState(args.ItemContainer, "CloseButtonDisabledState", ResourceHelper.AllowAnimation);
                    }
                }
            }
        }
    }


    #endregion

    #region RequireOpenTab

    static partial void OnRequireOpenTabChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TabView tabs)
        {
            tabs.TabItemsChanged -= TabItemsChanged;
            tabs.TabItemsChanged += TabItemsChanged;
        }

        static void TabItemsChanged(TabView sender, IVectorChangedEventArgs args)
        {
            var items = sender.GetFirstLevelDescendantsOfType<TabViewItem>().ToList();
            if (GetRequireOpenTab(sender) && sender.TabItems.Count == 1)
            {
                foreach (var item in items)
                    VisualStateManager.GoToState(item, "CloseButtonDisabledState", ResourceHelper.AllowAnimation);
            }
            else
            {
                foreach (var item in items)
                    VisualStateManager.GoToState(item, "CloseButtonEnabledState", ResourceHelper.AllowAnimation);
            }
        }
    }

    #endregion

    #region TargetContextFlyout

    static partial void OnTargetContextFlyoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement f)
        {
            f.ContextRequested -= ContextRequested;
            f.ContextRequested += ContextRequested;
        }

        static void ContextRequested(UIElement sender, Windows.UI.Xaml.Input.ContextRequestedEventArgs args)
        {
            if (GetTargetContextFlyout(sender) is FlyoutBase f)
            {
                args.TryGetPosition(sender, out Windows.Foundation.Point p);
                f.ShowAt(sender, new() { Position = p });
            }
        }
    }

    #endregion

    #region UseExpandContractAnimation

    static partial void OnUseExpandContractAnimationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FlyoutBase f)
        {
            f.Opened -= F_Opened;
            f.Opened += F_Opened;

            f.Closing -= F_Closing;
            f.Closing += F_Closing;

            if (e.NewValue is bool b)
                f.AreOpenCloseAnimationsEnabled = false;
        }

        /* 
         * N.B: Ideally these animations would be implemented using UIElement.SetShowAnimation(...)
         *      and UIElement.SetHideAnimation(...), however this can cause an infinite loop lock-up
         *      at the kernel level for some reason so we must manually play and control the animations.
         */

        static void F_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
        {
            FrameworkElement p = sender.GetPresenter();

            // 1. Check if we actually need to start an animation
            if (p is null
                || ResourceHelper.AllowAnimation is false
                || (p.Tag is string t && t == "Closed"))
                return;

            args.Cancel = true;

            // 2. If already closing, let the existing animation finish
            if (p.Tag is string tg && tg == "Closing")
                return;

            p.Tag = "Closing";

            Visual v = p.GetElementVisual();

            v.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation,
                b =>
                {
                    var popClose = v.GetCached("UECPopupContract", () =>
                    {
                        var ease = v.GetCached("ExitEase",
                            () => v.Compositor.CreateCubicBezierEasingFunction(0.7f, 0.0f, 1.0f, 0.5f));

                        var scale = v.CreateVector3KeyFrameAnimation(nameof(Visual.Scale))
                                        .AddScaleKeyFrame(0, 1)
                                        .AddKeyFrame(1.0f, "Vector3(Min(0.01, 20.0 / this.Target.Size.X), Min(0.01, 20.0 / this.Target.Size.Y), 1.0)", ease)
                                        .SetDuration(0.15);

                        var step = v.Compositor.CreateStepEasingFunction();
                        step.IsFinalStepSingleFrame = true;

                        var op = v.CreateScalarKeyFrameAnimation(nameof(Visual.Opacity))
                                    .AddKeyFrame(1, 0, step)
                                    .SetDuration(0.13);

                        return v.Compositor.CreateAnimationGroup(scale, op);
                    });

                    v.StartAnimation(popClose);
                },
                b =>
                {
                    if ((string)p.Tag == "Closing")
                    {
                        p.Tag = "Closed";
                        sender.Hide();
                    }
                });
        }

        static void F_Opened(object sender, object e)
        {
            if ((sender is FlyoutBase flyout
                && flyout.GetPresenter() is FrameworkElement presenter) is false)
                return;

            Visual v = presenter.GetElementVisual();
            presenter.Tag = "Opening";

            // 1. Disable animation if turned off
            if (GetUseExpandContractAnimation(flyout) is false
                || ResourceHelper.AllowAnimation is false)
            {
                presenter.SetHideAnimation(null);
                v.Scale = new(1f);
                v.Opacity = 1;
                presenter.Tag = "Open";
                return;
            }

            // 2. Set scale origin
            if (v.Scale.X == 1) // If animation has played previously scale will not be 1
            {
                if (presenter.RenderTransformOrigin.X != 0 || presenter.RenderTransformOrigin.Y != 0)
                {
                    CompositionFactory.StartCentering(
                        v, (float)presenter.RenderTransformOrigin.X, (float)presenter.RenderTransformOrigin.Y);
                }
            }

            // 3. Create Expand animation and play it
            var popOpen = v.GetCached("UECPopupExpand", () =>
            {
                var scale = v.CreateVector3KeyFrameAnimation(nameof(Visual.Scale))
                        .AddKeyFrame(0.0f, "Vector3(Min(0.01, 20.0 / this.Target.Size.X), Min(0.01, 20.0 / this.Target.Size.Y), 1.0)")
                        .AddScaleKeyFrame(1, 1, v.Compositor.GetCachedEntranceEase())
                        .SetDuration(0.3);

                var op = v.CreateScalarKeyFrameAnimation(nameof(Visual.Opacity))
                    .SetDuration(0.1)
                    .AddKeyFrame(0, 1)
                    .AddKeyFrame(1, 1);

                return v.Compositor.CreateAnimationGroup(scale, op);
            });

            v.StartAnimation(popOpen);
        }
    }

    #endregion

    #region Flyout

    static partial void OnFlyoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ButtonBase b)
        {
            b.Click -= ButtonFlyoutClick;
            b.Click += ButtonFlyoutClick;
        }

        static void ButtonFlyoutClick(object sender, RoutedEventArgs e)
        {
            if (sender is ButtonBase b && GetFlyout(b) is FlyoutBase fly)
                fly.ShowAt(b);
        }
    }

    #endregion

    #region UseStandardReposition

    static partial void OnUseStandardRepositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement f && e.NewValue is bool b)
        {
            if (b)
                CompositionFactory.SetStandardReposition(f);
            else
                CompositionFactory.DisableStandardReposition(f);
        }
    }

    #endregion

    #region SupportAnimatedIcon

    static partial void OnSupportAnimatedIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs a)
    {
        if (d is FrameworkElement e)
        {
            AnimatedIcon.SetState(e, "Normal");

            e.RemoveHandler(UIElement.PointerPressedEvent, (PointerEventHandler)PointerPressed);
            e.RemoveHandler(UIElement.PointerReleasedEvent, (PointerEventHandler)PointerReleased);

            e.PointerEntered -= PointerEntered;
            e.PointerExited -= PointerReleased;

            if (a.NewValue is bool b && b)
            {
                e.AddHandler(FrameworkElement.PointerPressedEvent, new PointerEventHandler(PointerPressed), true);
                e.AddHandler(FrameworkElement.PointerReleasedEvent, new PointerEventHandler(PointerReleased), true);

                e.PointerEntered += PointerEntered;
                e.PointerExited += PointerReleased;
            }
        }

        static void PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (ResourceHelper.AllowAnimation)
                AnimatedIcon.SetState((FrameworkElement)sender, "Pressed");
        }

        static void PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (ResourceHelper.AllowAnimation)
                AnimatedIcon.SetState((FrameworkElement)sender, "PointerOver");
        }

        static void PointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState((FrameworkElement)sender, "Normal");
        }
    }

    #endregion

    #region AttachedStates

    static partial void OnUseAttachedStatesChanged(DependencyObject d, DependencyPropertyChangedEventArgs a)
    {
        if (d is FrameworkElement e)
        {
            e.RemoveHandler(UIElement.PointerPressedEvent, (PointerEventHandler)PointerPressed);
            e.RemoveHandler(UIElement.PointerReleasedEvent, (PointerEventHandler)PointerReleased);

            e.PointerEntered -= PointerEntered;
            e.PointerExited -= PointerExited;

            if (a.NewValue is bool b && b)
            {
                e.AddHandler(FrameworkElement.PointerPressedEvent, new PointerEventHandler(PointerPressed), true);
                e.AddHandler(FrameworkElement.PointerReleasedEvent, new PointerEventHandler(PointerReleased), true);

                e.PointerEntered += PointerEntered;
                e.PointerExited += PointerExited;
            }

            static bool TryStartState(FrameworkElement e, string key)
            {
                if (ResourceHelper.Get<Storyboard>(e, key) is Storyboard state)
                {
                    state.Begin();
                    if (ResourceHelper.AllowAnimation is false)
                        state.SkipToFill();
                    return true;
                }

                return false;
            }

            static void PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs _)
            {
                if (sender is FrameworkElement e && Properties.GetUseAttachedStates(e))
                    TryStartState(e, "Pressed");
            }

            static void PointerEntered(object sender, PointerRoutedEventArgs _)
            {
                if (sender is FrameworkElement e && Properties.GetUseAttachedStates(e))
                    TryStartState(e, "PointerOver");
            }

            static void PointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs _)
            {
                if (sender is FrameworkElement e && Properties.GetUseAttachedStates(e))
                    TryStartState(e, "Released");
            }

            static void PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs _)
            {
                if (sender is FrameworkElement e && Properties.GetUseAttachedStates(e))
                    TryStartState(e, "Normal");
            }
        }
    }

    #endregion

    #region UseClickAnimation

    static partial void OnClickAnimationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement f)
        {
            // 1. Remove old handlers
            if (f is ButtonBase b)
                b.Click -= OnClick;

            f.RemoveHandler(UIElement.PointerReleasedEvent, (PointerEventHandler)Clicky);

            // 2. Add new handlers
            if (e.NewValue is string s && !string.IsNullOrWhiteSpace(s))
            {
                if (f is ButtonBase bu)
                    bu.Click += OnClick;
                else
                    f.AddHandler(FrameworkElement.PointerReleasedEvent, new PointerEventHandler(Clicky), true);
            }

            static void Clicky(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs _)
            {
                if (sender is FrameworkElement e
                    && ResourceHelper.AllowAnimation
                    && ResourceHelper.SupportFluentAnimation
                    && Properties.GetClickAnimation(e) is string s
                    && !string.IsNullOrWhiteSpace(s))
                    CompButtonAnimate(e, s, GetClickAnimationOffset(e));
            }

            static void OnClick(object sender, RoutedEventArgs _)
            {
                if (sender is FrameworkElement e
                     && ResourceHelper.AllowAnimation
                     && ResourceHelper.SupportFluentAnimation
                     && Properties.GetClickAnimation(e) is string s
                     && !string.IsNullOrWhiteSpace(s))
                    CompButtonAnimate(e, s, GetClickAnimationOffset(e));
            }
        }
    }

    #endregion

    #region PointerOverAnimation

    static void CompButtonAnimate(FrameworkElement source, string key, double offset, bool over = false)
    {
        var parts = key.Split("|");
        var targets = parts[0].Split(",");

        foreach (var src in targets)
        {
            if (source.GetDescendantsOfType<FrameworkElement>()
                .FirstOrDefault(fe => fe.Name == src) is FrameworkElement target)
            {
                if (parts.Length > 1 && parts[1] == "Scale")
                {
                    Visual v = target.GetElementVisual();
                    v.StartAnimation(FluentAnimationHelper.CreatePointerUp(v));
                }
                else
                {
                    Storyboard sb = new();
                    var ease = new ElasticEase { Oscillations = 2, Springiness = 5, EasingMode = EasingMode.EaseOut };

                    bool hasPressed = string.IsNullOrEmpty(GetPointerPressedAnimation(source)) is false;
                    double duration = hasPressed ? 0.35 : 0.5;

                    // Create translate animation
                    string path = parts.Length > 1 && parts[1] == "X"
                        ? TargetProperty.CompositeTransform.TranslateX
                        : TargetProperty.CompositeTransform.TranslateY;

                    var t = sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(target, path);
                    if (over || hasPressed is false)
                    {
                        if (offset == 0)
                            t.AddKeyFrame(0.8, offset, ease);
                        else
                            t.AddKeyFrame(0.15, offset);
                    }

                    sb.Begin();
                }
            }
        }

       
    }

    static partial void OnPointerOverAnimationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement f)
        {
            // 1. Remove old handlers
            f.RemoveHandler(UIElement.PointerEnteredEvent, (PointerEventHandler)PointerOverEntered);
            f.RemoveHandler(UIElement.PointerExitedEvent, (PointerEventHandler)PointerOverExited);
            f.RemoveHandler(UIElement.PointerCaptureLostEvent, (PointerEventHandler)PointerOverExited);

            // 2. Add new handlers
            if (e.NewValue is string s && !string.IsNullOrWhiteSpace(s))
            {
                f.AddHandler(FrameworkElement.PointerEnteredEvent, new PointerEventHandler(PointerOverEntered), true);
                f.AddHandler(FrameworkElement.PointerExitedEvent, new PointerEventHandler(PointerOverExited), true);
                f.AddHandler(FrameworkElement.PointerCaptureLostEvent, new PointerEventHandler(PointerOverExited), true);

            }

            static void PointerOverEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs _)
            {
                if (sender is FrameworkElement e
                    && ResourceHelper.AllowAnimation
                    && ResourceHelper.SupportFluentAnimation
                    && ResourceHelper.UsePointerOverAnimations
                    && Properties.GetPointerOverAnimation(e) is string s
                    && !string.IsNullOrWhiteSpace(s))
                    CompButtonAnimate(e, s, GetPointerAnimationOffset(e), true);
            }

            static void PointerOverExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs _)
            {
                if (sender is FrameworkElement e
                    && ResourceHelper.AllowAnimation
                    && ResourceHelper.SupportFluentAnimation
                    && ResourceHelper.UsePointerOverAnimations
                    && Properties.GetPointerOverAnimation(e) is string s
                    && !string.IsNullOrWhiteSpace(s))
                    CompButtonAnimate(e, s, 0, true);
            }
        }
    }

    #endregion

    #region PointerPressedAnimation

    static partial void OnPointerPressedAnimationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement f)
        {
            // 1. Remove old handlers
            f.RemoveHandler(UIElement.PointerPressedEvent, (PointerEventHandler)PointerPressed);
            f.RemoveHandler(UIElement.PointerExitedEvent, (PointerEventHandler)PointerExited);

            // 2. Add new handlers
            if (e.NewValue is string s && !string.IsNullOrWhiteSpace(s))
            {
                f.AddHandler(FrameworkElement.PointerPressedEvent, new PointerEventHandler(PointerPressed), true);
                f.AddHandler(FrameworkElement.PointerExitedEvent, new PointerEventHandler(PointerExited), true);
            }

            static void PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs _)
            {
                if (sender is FrameworkElement e
                    && ResourceHelper.AllowAnimation
                    && ResourceHelper.SupportFluentAnimation
                    && Properties.GetPointerPressedAnimation(e) is string s
                    && !string.IsNullOrWhiteSpace(s))
                    DoAnimate(e, s, GetClickAnimationOffset(e));
            }

            static void PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs _)
            {
                if (sender is FrameworkElement e
                    && ResourceHelper.AllowAnimation
                    && ResourceHelper.SupportFluentAnimation
                    && Properties.GetPointerPressedAnimation(e) is string s
                    && !string.IsNullOrWhiteSpace(s))
                    RestoreAnimate(e, s);
            }

            static void RestoreAnimate(FrameworkElement source, string key)
            {
                var parts = key.Split("|");
                if (source.GetDescendantsOfType<FrameworkElement>()
                    .FirstOrDefault(fe => fe.Name == parts[0]) is FrameworkElement target)
                {
                    double duration = 0.35;

                    if (parts.Length > 1 && parts[1] == "Scale")
                    {
                        Visual v = target.GetElementVisual();
                        v.StartAnimation(FluentAnimationHelper.CreatePointerUp(v));
                    }
                    else
                    {
                        Storyboard sb = new();
                        var ease = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut };
                        // Create translate animation
                        var t = sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(target, TargetProperty.CompositeTransform.TranslateY)
                            .AddKeyFrame(duration, 0, ease);
                        sb.Begin();

                    }
                }
            }

            static void DoAnimate(FrameworkElement source, string key, double offset)
            {
                var parts = key.Split("|");
                if (source.GetDescendantsOfType<FrameworkElement>()
                    .FirstOrDefault(fe => fe.Name == parts[0]) is FrameworkElement target)
                {

                    if (parts.Length > 1 && parts[1] == "Scale")
                    {
                        Visual v = target.GetElementVisual();
                        v.StartAnimation(FluentAnimationHelper.CreatePointerDown(v, (float)offset));
                    }
                    else
                    {
                        // Create translate animation
                        Storyboard sb = new();
                        var ease = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut };
                        sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(target, TargetProperty.CompositeTransform.TranslateY)
                            .AddKeyFrame(0.15, offset)
                            .AddKeyFrame(0.5, 0, ease);

                        sb.Begin();
                    }
                }
            }
        }
    }

    #endregion

    #region ItemContainerBackgroundTransition

    static partial void OnItemContainerBackgroundTransitionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListViewBase b)
        {
            b.ContainerContentChanging -= ContainerContentChanging;
            b.ContainerContentChanging += ContainerContentChanging;
        }

        static void ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.ItemContainer is not null
                && GetSetContainerBackgroundTransition(args.ItemContainer) is false
                && args.ItemContainer.ContentTemplateRoot is not null)
            {
                SetSetContainerBackgroundTransition(args.ItemContainer, true);

                var c = VisualTreeHelper.GetChild(args.ItemContainer, 0);
                var b = VisualTreeHelper.GetChild(c, 0);
                if (b is Border br && br.BackgroundTransition is null)
                    br.BackgroundTransition = ResourceHelper.AllowAnimation ? GetItemContainerBackgroundTransition(sender) as BrushTransition : null;
            }
        }
    }

    #endregion

    #region Uppercase

    static partial void OnUppercaseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock t && e.NewValue is string s)
            t.Text = (s ?? string.Empty).ToUpper();
    }

    #endregion

    #region DisableTranslation 

    static partial void OnDisableTranslationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Fix offset ComboBox's caused by global PerspectiveTransform3D
        if (d is Popup popup && e.NewValue is bool s && s)
        {
            popup.Opened += async (src, arg) =>
            {
                //await Task.Delay(32);
                if (src is Popup pp && pp.Child?.GetFirstDescendantOfType<Border>() is Border b)
                    b.Translation = new();
            };
        }
    }

    #endregion

    #region RenderScale

    static partial void OnRenderScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement f && e.NewValue is double s)
        {
            var ct = f.GetCompositeTransform();
            ct.ScaleX = ct.ScaleY = s;
        }
    }

    #endregion

    #region Rotation

    static partial void OnRotationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement f && f.GetElementVisual() is { } v && e.NewValue is double n)
        {
            v.CenterPoint = new(v.Size / 2f, 0f);
            v.RotationAxis = Vector3.UnitZ;
            v.RotationAngleInDegrees = (float)n;
        }
    }

    static partial void OnRotationTransitionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement f
                 && f.GetElementVisual() is { } v
                 && e.NewValue is KeyTime n)
        {
            v.SetImplicitAnimation(nameof(Visual.RotationAngleInDegrees),
                v.CreateScalarKeyFrameAnimation(nameof(Visual.RotationAngleInDegrees))
                .AddKeyFrame(0, CompositionFactory.STARTING_VALUE)
                .AddKeyFrame(1, CompositionFactory.FINAL_VALUE)
                .SetDuration(n.TimeSpan));
        }
    }

    #endregion

    #region RichEditBox Text, FontStretch, FontWeight, FontStyle

    static partial void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RichEditBox r)
        {
            r.TextChanged -= RichEditBox_TextChanged;

            r.TextDocument.GetText(TextGetOptions.None, out string t);

            if (t.Trim() == ((string)e.NewValue).Trim())
                return;

            r.TextDocument.SetText(Windows.UI.Text.TextSetOptions.None, e.NewValue as string);
            UpdateFormat(r);
            r.TextChanged += RichEditBox_TextChanged;
        }
    }

    private static void RichEditBox_TextChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RichEditBox r)
        {
            r.TextDocument.GetText(TextGetOptions.None, out string t);
            SetText(r, t.TrimEnd('\r', '\n'));
        }
    }

    static partial void OnFontFamilyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RichEditBox r)
            UpdateFormat(r);
    }

    static partial void OnFontStretchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RichEditBox r)
            UpdateFormat(r);
    }

    static partial void OnFontStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RichEditBox r)
            UpdateFormat(r);
    }

    static partial void OnFontWeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RichEditBox r)
            UpdateFormat(r);
    }

    public static Dictionary<CoreCursorType, CoreCursor> Cursors => _cursors;

    static void UpdateFormat(RichEditBox r)
    {
        // This is *NOT* good for performance, but RichEditBox has a lot of problems
        // we need to workaround. It is however the only way we can actually get proper
        // font display on a TextBox, so we need to deal with it.

        r.UpdateLayout();

        r.TextDocument.BatchDisplayUpdates();

        r.FontFamily = ResourceHelper.Get<FontFamily>("BLANK");

        ITextCharacterFormat format = r.TextDocument.GetDefaultCharacterFormat();
        format.FontStyle = GetFontStyle(r);
        format.FontStretch = GetFontStretch(r);
        format.Weight = GetFontWeight(r).Weight;
        r.TextDocument.SetDefaultCharacterFormat(format);

        r.TextDocument.ApplyDisplayUpdates();
        r.FontFamily = GetFontFamily(r);

        r.UpdateLayout();

    }

    #endregion

    #region ToolTip MemberPath & Template

    static partial void OnToolTipMemberPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListViewBase lvb)
        {
            // 1. Ensure content changes during recycling
            lvb.ContainerContentChanging -= ContainerContentChanging;
            lvb.ContainerContentChanging += ContainerContentChanging;

            // 2. Update any existing containers
            if (lvb.ItemsPanelRoot is null)
                return;
            string path = e.NewValue.ToString();
            foreach (var item in lvb.ItemsPanelRoot.Children.Cast<SelectorItem>())
                if (item.Content is not null)
                    Set(lvb, item, path);

            static void Set(ListViewBase sender, SelectorItem item, string path = null)
            {
                path ??= GetToolTipMemberPath(sender);

                if (string.IsNullOrWhiteSpace(path))
                    item.ClearValue(ToolTipService.ToolTipProperty);
                else
                {
                    Binding b = new()
                    {
                        Source = item,
                        Path = new($"Content.{path}")
                    };

                    // Hack to support overriding tooltip style with MUXC themes
                    if (ResourceHelper.TryGet("DefaultThemeToolTipStyle", out Style style))
                    {
                        ToolTip t = new() { Style = style };
                        t.SetBinding(ToolTip.ContentProperty, b);
                        ToolTipService.SetToolTip(item, t);
                    }
                    else
                    {
                        // Fast path
                        item.SetBinding(ToolTipService.ToolTipProperty, b);
                    }
                }
            }

            static void ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
            {
                if (args.ItemContainer is null)
                    return;

                string path = GetToolTipMemberPath(sender);
                if (args.InRecycleQueue || string.IsNullOrWhiteSpace(path))
                    Set(sender, args.ItemContainer, string.Empty);
                else
                    Set(sender, args.ItemContainer, path);
            }
        }
    }

    // Provides template-able ToolTips for ListViewItems
    static partial void OnToolTipTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListViewBase lvb)
        {
            // 1. Ensure content changes during recycling
            lvb.ContainerContentChanging -= ContainerContentChanging;
            lvb.ContainerContentChanging += ContainerContentChanging;

            // 2. Update any existing containers
            if (lvb.ItemsPanelRoot is null )
                return;
            string path = e.NewValue.ToString();
            foreach (var item in lvb.ItemsPanelRoot.Children.Cast<SelectorItem>())
                if (ToolTipService.GetToolTip(item) is ToolTip t)
                    t.Placement = GetToolTipPlacement(lvb);

            static void ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
            {
                if (args.ItemContainer is null)
                    return;

                if (args.InRecycleQueue)
                {
                    if (ToolTipService.GetToolTip(args.ItemContainer) is ToolTip rtt)
                        rtt.Opened -= OnToolTipOpened;
                    return;
                }

                // TabViewItem's create their own blank ToolTip, so we specifically 
                // want to override that default empty ToolTip so we can show our own.
                bool tabViewHack = sender is TabViewListView;

                if (ToolTipService.GetToolTip(args.ItemContainer) is not ToolTip t
                    // Default TabViewItem TT is disabled and empty
                    || (tabViewHack && t.IsEnabled is false && t.Content is null))
                {
                    t = new() { Tag = sender };

                    if (ResourceHelper.TryGet("DefaultThemeToolTipStyle", out Style style))
                        t.Style = style;

                    t.Tag = sender;

                    t.Placement = GetToolTipPlacement(sender);
                    ToolTipService.SetToolTip(args.ItemContainer, t);
                }

                t.Opened -= OnToolTipOpened;
                t.Opened += OnToolTipOpened;


                SetTag(t, args.ItemContainer);
            }

            static void OnToolTipOpened(object sender, RoutedEventArgs e)
            {
                if (sender is ToolTip t
                    && t.Tag is ListViewBase lb
                    && GetTag(t) is SelectorItem item
                    && GetToolTipTemplate(lb) is DataTemplate template)
                {
                    var content = (FrameworkElement)template.LoadContent();
                    content.DataContext = item.DataContext ?? item.Content;
                    t.Content = content;
                }
            }
        }
    }

    static async partial void OnToolTipStyleKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // MUXC styles don't allow overriding default tooltip style in XAML
        // so we use this code to force
        if (e.NewValue is string key && !string.IsNullOrWhiteSpace(key)
            && ResourceHelper.TryGet(key, out Style style))
        {
            if (d is ToolTip st)
            {
                st.Style = style;
                return;
            }

            var temp = ToolTipService.GetToolTip(d);
            if (temp is null) // Bad little hack to allow x:UID setters (or equivalent) to run
                await Task.Yield();
            
            if (ToolTipService.GetToolTip(d) is { } tt)
            {
                if (tt is ToolTip t)
                    t.Style = style;
                else
                {
                    // Most of our tooltips will be strings set automatically
                    // by x:UID, so we need to create a shell ToolTip around
                    // them to place the style on.
                    ToolTipService.SetToolTip(d, null);
                    t = new ToolTip();
                    t.Content = tt;
                    t.Placement = ToolTipService.GetPlacement(d);
                    t.Style = style;

                    ToolTipService.SetToolTip(d, t);
                }
            }
        }
    }

    static partial void OnToolTipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Workaround an MUXC bug that does not allow overriding ToolTip style in XAML
        if (ResourceHelper.TryGet("DefaultThemeToolTipStyle", out Style style))
        {
            if (ToolTipService.GetToolTip(d) is not ToolTip t)
            {
                t = new();
                ToolTipService.SetToolTip(d, null);
            }

            t.Style = style;
            t.Content = e.NewValue;

            ToolTipService.SetToolTip(d, t);
        }
        else
        {
            // Fast path
            ToolTipService.SetToolTip(d, e.NewValue);
        }
    }

    #endregion

    #region GridDefinitions

    static partial void OnGridDefinitionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement f && e.NewValue is string s)
        {
            char c = s.Contains(",") ? ',' : ' ';
            string[] parts = s.Split(c, StringSplitOptions.RemoveEmptyEntries);

            Grid g = f as Grid;
            g?.RowDefinitions?.Clear();
            g?.ColumnDefinitions?.Clear();

            foreach (var part in parts)
            {
                if (g is not null)
                {
                    // Create Grid Column or Row definitions

                    if (part.StartsWith("cs"))
                    {
                        g.ColumnSpacing = Convert.ToDouble(part.Remove(0, 2));
                    }
                    else if (part.StartsWith("rs"))
                    {
                        g.RowSpacing = Convert.ToDouble(part.Remove(0, 2));
                    }
                    else if (part.StartsWith('c'))
                    {
                        var p = part.Remove(0, 1);
                        ColumnDefinition cd = new();
                        if (p == "*")
                            cd.Width = new(1, GridUnitType.Star);
                        else if (p.EndsWith("*"))
                            cd.Width = new(Convert.ToDouble(p.Remove(p.Length - 1)), GridUnitType.Star);
                        else if (p == "Auto")
                            cd.Width = GridLength.Auto;
                        else
                            cd.Width = new(Convert.ToDouble(p));

                        g.ColumnDefinitions.Add(cd);
                    }
                    else if (part.StartsWith('r'))
                    {
                        var p = part.Remove(0, 1);
                        RowDefinition cd = new();
                        if (p == "*")
                            cd.Height = new(1, GridUnitType.Star);
                        else if (p.EndsWith("*"))
                            cd.Height = new(Convert.ToDouble(p.Remove(p.Length - 1)), GridUnitType.Star);
                        else if (p == "Auto")
                            cd.Height = GridLength.Auto;
                        else
                            cd.Height = new(Convert.ToDouble(p));

                        g.RowDefinitions.Add(cd);
                    }
                }
                else
                {
                    // Set Column or Row attached properties
                    if (part.StartsWith('c'))
                    {
                        string p = part.Remove(0, 1);
                        Grid.SetColumn(f, Convert.ToInt32(p));
                    }
                    else if (part.StartsWith('r'))
                    {
                        string p = part.Remove(0, 1);
                        Grid.SetRow(f, Convert.ToInt32(p));
                    }
                    if (part.StartsWith("cs"))
                    {
                        string p = part.Remove(0, 2);
                        Grid.SetColumnSpan(f, Convert.ToInt32(p));
                    }
                    else if (part.StartsWith("rs"))
                    {
                        string p = part.Remove(0, 2);
                        Grid.SetRowSpan(f, Convert.ToInt32(p));
                    }
                }
            }

            f.InvalidateArrange();
        }
    }

    #endregion

    #region Hyperlink

    static partial void OnHyperlinkChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ButtonBase b)
        {
            b.Click -= B_Click;
            b.Click += B_Click;

            static void B_Click(object sender, RoutedEventArgs e)
            {
                var link = GetHyperlink((DependencyObject)sender);
                if (!string.IsNullOrWhiteSpace(link) 
                    && Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out Uri uri))
                    _ = Launcher.LaunchUriAsync(uri);
            }
        }
    }

    #endregion

    #region Cursor

    private static readonly object _cursorLock = new ();
    private static readonly CoreCursor _defaultCursor = new (CoreCursorType.Arrow, 1);
    private static readonly Dictionary<CoreCursorType, CoreCursor> _cursors = new () { { CoreCursorType.Arrow, _defaultCursor } };

    static partial void OnCursorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;

        var value = (CoreCursorType)e.NewValue;

        // lock ensures CoreCursor creation and event handlers attachment/detachment is atomic
        lock (_cursorLock)
        {
            if (!Cursors.ContainsKey(value))
                Cursors[value] = new CoreCursor(value, 1);

            // make sure event handlers are not attached twice to element
            element.PointerEntered -= Element_PointerEntered;
            element.PointerEntered += Element_PointerEntered;
            element.PointerExited -= Element_PointerExited;
            element.PointerExited += Element_PointerExited;
            element.Unloaded -= ElementOnUnloaded;
            element.Unloaded += ElementOnUnloaded;
        }

        static void Element_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            CoreCursorType cursor = GetCursor((FrameworkElement)sender);
            Window.Current.CoreWindow.PointerCursor = Cursors[cursor];
        }

        static void Element_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            // when exiting change the cursor to the target Mouse.Cursor value of the new element
            CoreCursor cursor;
            if (e.OriginalSource is FrameworkElement newElement)
                cursor = Cursors[GetCursor(newElement)];
            else
                cursor = _defaultCursor;

            Window.Current.CoreWindow.PointerCursor = cursor;
        }

        static void ElementOnUnloaded(object sender, RoutedEventArgs routedEventArgs)
        {
            // when the element is programatically unloaded, reset the cursor back to default
            // this is necessary when click triggers immediate change in layout and PointerExited is not called
            Window.Current.CoreWindow.PointerCursor = _defaultCursor;
        }
    }

    #endregion
}
