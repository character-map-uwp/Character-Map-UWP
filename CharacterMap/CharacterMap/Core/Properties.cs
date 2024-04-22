using CharacterMap.Controls;
using CharacterMapCX.Controls;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation.Collections;
using Windows.UI.Composition;
using Windows.UI.Core;
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

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class DependencyPropertyAttribute<T> : Attribute
{
    public string Name { get; set; }
    public object Default { get; set; }
    public Type Type => typeof(T);

    public DependencyPropertyAttribute() { }

    public DependencyPropertyAttribute(string name)
    {
        Name = name;
    }

    public DependencyPropertyAttribute(string name, object def)
    {
        Name = name;
        Default = def;
    }
}

public class DependencyPropertyAttribute : DependencyPropertyAttribute<object> {
    public DependencyPropertyAttribute() { }
    public DependencyPropertyAttribute(string name)
    {
        Name = name;
    }

    public DependencyPropertyAttribute(string name, object def)
    {
        Name = name;
        Default = def;
    }
}
    

/// <summary>
/// XAML Attached Properties
/// </summary>
[Bindable]
public class Properties : DependencyObject
{
    #region FILTER 

    /* Used to apply a filter to MenuFlyoutItem's on the filter list */

    public static BasicFontFilter GetFilter(DependencyObject obj)
    {
        return (BasicFontFilter)obj.GetValue(FilterProperty);
    }

    public static void SetFilter(DependencyObject obj, BasicFontFilter value)
    {
        obj.SetValue(FilterProperty, value);
    }

    public static readonly DependencyProperty FilterProperty =
        DependencyProperty.RegisterAttached("Filter", typeof(BasicFontFilter), typeof(Properties), new PropertyMetadata(null, (d, e) =>
        {
            if (d is MenuFlyoutItem item && e.NewValue is BasicFontFilter f)
            {
                item.CommandParameter = f;
                item.Text = f.DisplayTitle;
            }
        }));

    #endregion

    #region TYPOGRAPHY

    /* Helper to apply TypographyFeatureInfo to a TextBlock */

    public static TypographyFeatureInfo GetTypography(DependencyObject obj)
    {
        return (TypographyFeatureInfo)obj.GetValue(TypographyProperty);
    }

    public static void SetTypography(DependencyObject obj, TypographyFeatureInfo value)
    {
        obj.SetValue(TypographyProperty, value);
    }

    public static readonly DependencyProperty TypographyProperty =
        DependencyProperty.RegisterAttached("Typography", typeof(TypographyFeatureInfo), typeof(Properties), new PropertyMetadata(null, (d, e) =>
        {
            if (d is TextBlock t)
            {
                TypographyFeatureInfo i = e.NewValue as TypographyFeatureInfo;
                var x = XamlDirect.GetDefault();
                IXamlDirectObject p = x.GetXamlDirectObject(t);
                GridViewHelper.UpdateTypography(x, p, i);
            }
        }));

    #endregion

    #region CLIP TO BOUNDS

    public static bool GetClipToBounds(DependencyObject obj)
    {
        return (bool)obj.GetValue(ClipToBoundsProperty);
    }

    public static void SetClipToBounds(DependencyObject obj, bool value)
    {
        obj.SetValue(ClipToBoundsProperty, value);
    }

    public static readonly DependencyProperty ClipToBoundsProperty =
        DependencyProperty.RegisterAttached("ClipToBounds", typeof(bool), typeof(Properties), new PropertyMetadata(false, (d, e) =>
        {
            if (d is FrameworkElement f)
            {
                var v = f.GetElementVisual();
                if (e.NewValue is true)
                    v.Clip = v.Compositor.CreateInsetClip();
                else
                    v.Clip = null;

            }
        }));

    #endregion

    #region ICON

    public static IconElement GetIcon(DependencyObject obj)
    {
        return (IconElement)obj.GetValue(IconProperty);
    }

    public static void SetIcon(DependencyObject obj, IconElement value)
    {
        obj.SetValue(IconProperty, value);
    }

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.RegisterAttached("Icon", typeof(IconElement), typeof(Properties), new PropertyMetadata(null));

    #endregion

    #region PopupRoot

    public static FrameworkElement GetPopupRoot(DependencyObject obj)
    {
        return (FrameworkElement)obj.GetValue(PopupRootProperty);
    }

    public static void SetPopupRoot(DependencyObject obj, FrameworkElement value)
    {
        obj.SetValue(PopupRootProperty, value);
    }

    public static readonly DependencyProperty PopupRootProperty =
        DependencyProperty.RegisterAttached("PopupRoot", typeof(FrameworkElement), typeof(Properties), new PropertyMetadata(0));

    #endregion

    #region DEVOPTION

    public static DevOption GetDevOption(DependencyObject obj)
    {
        return (DevOption)obj.GetValue(DevOptionProperty);
    }

    public static void SetDevOption(DependencyObject obj, DevOption value)
    {
        obj.SetValue(DevOptionProperty, value);
    }

    public static readonly DependencyProperty DevOptionProperty =
        DependencyProperty.RegisterAttached("DevOption", typeof(DevOption), typeof(Properties), new PropertyMetadata(null));


    #endregion

    #region StyleKey


    public static string GetStyleKey(DependencyObject obj)
    {
        return (string)obj.GetValue(StyleKeyProperty);
    }

    public static void SetStyleKey(DependencyObject obj, string value)
    {
        obj.SetValue(StyleKeyProperty, value);
    }

    public static readonly DependencyProperty StyleKeyProperty =
        DependencyProperty.RegisterAttached("StyleKey", typeof(string), typeof(Properties), new PropertyMetadata(null));

    #endregion

    #region IsMouseInputEnabled

    /// Enables Mouse & Touch input on an InkCanvas

    public static bool GetIsMouseInputEnabled(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsMouseInputEnabledProperty);
    }

    public static void SetIsMouseInputEnabled(DependencyObject obj, bool value)
    {
        obj.SetValue(IsMouseInputEnabledProperty, value);
    }

    public static readonly DependencyProperty IsMouseInputEnabledProperty =
        DependencyProperty.RegisterAttached("IsMouseInputEnabled", typeof(bool), typeof(Properties), new PropertyMetadata(false, (d, e) =>
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
        }));

    #endregion

    #region DirectText Options

    // Applies CharacterRenderingOptions to a DirectText control

    public static CharacterRenderingOptions GetOptions(DependencyObject obj)
    {
        return (CharacterRenderingOptions)obj.GetValue(OptionsProperty);
    }

    /// <summary>
    /// Applies <see cref="CharacterRenderingOptions"/> to a <see cref="DirectText"/> control
    /// </summary>
    public static void SetOptions(DependencyObject obj, CharacterRenderingOptions value)
    {
        obj.SetValue(OptionsProperty, value);
    }

    /// <summary>
    /// Applies <see cref="CharacterRenderingOptions"/> to a <see cref="DirectText"/> or <see cref="TextBlock"/> control
    /// </summary>
    public static readonly DependencyProperty OptionsProperty =
        DependencyProperty.RegisterAttached("Options", typeof(CharacterRenderingOptions), typeof(Properties), new PropertyMetadata(null, (s, e) =>
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
        }));

    #endregion

    #region DEFAULT TOOL

    /// Sets the default tool for an InkToolbar

    public static InkToolbarToolButton GetDefaultTool(DependencyObject obj)
    {
        return (InkToolbarToolButton)obj.GetValue(DefaultToolProperty);
    }

    public static void SetDefaultTool(DependencyObject obj, InkToolbarToolButton value)
    {
        obj.SetValue(DefaultToolProperty, value);
    }

    public static readonly DependencyProperty DefaultToolProperty =
        DependencyProperty.RegisterAttached("DefaultTool", typeof(InkToolbarToolButton), typeof(Properties), new PropertyMetadata(null, (d, e) =>
        {
            if (d is InkToolbar t)
            {
                if (e.NewValue is InkToolbarToolButton b)
                    t.ActiveTool = b;
            }
        }));

    #endregion

    #region InsetClip

    public static Thickness GetInsetClip(DependencyObject obj)
    {
        return (Thickness)obj.GetValue(InsetClipProperty);
    }

    public static void SetInsetClip(DependencyObject obj, Thickness value)
    {
        obj.SetValue(InsetClipProperty, value);
    }

    public static readonly DependencyProperty InsetClipProperty =
        DependencyProperty.RegisterAttached("InsetClip", typeof(Thickness), typeof(Properties), new PropertyMetadata(new Thickness(0d), (d, e) =>
        {
            if (d is FrameworkElement f && e.NewValue is Thickness t)
            {
                Visual v = f.GetElementVisual();
                if (t.Left > 0 || t.Right > 0 || t.Top > 0 || t.Bottom > 0)
                    v.Clip = v.Compositor.CreateInsetClip((float)t.Left, (float)t.Top, (float)t.Right, (float)t.Bottom);
                else
                    v.Clip = null;
            }
        }));

    #endregion

    #region TabViewItem IsCompact

    public static bool GetIsCompact(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsCompactProperty);
    }

    public static void SetIsCompact(DependencyObject obj, bool value)
    {
        obj.SetValue(IsCompactProperty, value);
    }

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.RegisterAttached("IsCompact", typeof(bool), typeof(Properties), new PropertyMetadata(false, (d, e) =>
        {
            if (d is TabViewItem item && e.NewValue is bool b)
            {
                VisualStateManager.GoToState(item, b ? "CollapsedTabState" : "FullTabState", ResourceHelper.AllowAnimation);

                if (b)
                    item.MaxWidth = 60;
                else
                    item.ClearValue(TabViewItem.MaxWidthProperty);
            }
        }));

    #endregion

    #region IsTabOpenAnimationEnabled

    public static bool GetIsTabOpenAnimationEnabled(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsTabOpenAnimationEnabledProperty);
    }

    public static void SetIsTabOpenAnimationEnabled(DependencyObject obj, bool value)
    {
        obj.SetValue(IsTabOpenAnimationEnabledProperty, value);
    }

    public static readonly DependencyProperty IsTabOpenAnimationEnabledProperty =
        DependencyProperty.RegisterAttached("IsTabOpenAnimationEnabled", typeof(bool), typeof(Properties), new PropertyMetadata(false, (d, e) =>
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
        }));

    #endregion

    #region RequireOpenTab

    public static bool GetRequireOpenTab(DependencyObject obj)
    {
        return (bool)obj.GetValue(RequireOpenTabProperty);
    }

    public static void SetRequireOpenTab(DependencyObject obj, bool value)
    {
        obj.SetValue(RequireOpenTabProperty, value);
    }

    public static readonly DependencyProperty RequireOpenTabProperty =
        DependencyProperty.RegisterAttached("RequireOpenTab", typeof(bool), typeof(Properties), new PropertyMetadata(false, (d, e) =>
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
        }));

    #endregion

    #region TargetContextFlyout

    public static FlyoutBase GetTargetContextFlyout(DependencyObject obj)
    {
        return (FlyoutBase)obj.GetValue(TargetContextFlyoutProperty);
    }

    public static void SetTargetContextFlyout(DependencyObject obj, FlyoutBase value)
    {
        obj.SetValue(TargetContextFlyoutProperty, value);
    }

    public static readonly DependencyProperty TargetContextFlyoutProperty =
        DependencyProperty.RegisterAttached("TargetContextFlyout", typeof(FlyoutBase), typeof(Properties), new PropertyMetadata(null, (d, e) =>
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
        }));

    #endregion

    #region UseExpandContractAnimation

    public static bool GetUseExpandContractAnimation(DependencyObject obj)
    {
        return (bool)obj.GetValue(UseExpandContractAnimationProperty);
    }

    public static void SetUseExpandContractAnimation(DependencyObject obj, bool value)
    {
        obj.SetValue(UseExpandContractAnimationProperty, value);
    }

    public static readonly DependencyProperty UseExpandContractAnimationProperty =
        DependencyProperty.RegisterAttached("UseExpandContractAnimation", typeof(bool), typeof(Properties), new PropertyMetadata(null, (d, e) =>
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
             *      and UIElement.SetHideAnimation(...), however this can cause an infinite loop lockup
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
        }));

    #endregion

    #region UseStandardReposition

    public static bool GetUseStandardReposition(DependencyObject obj)
    {
        return (bool)obj.GetValue(UseStandardRepositionProperty);
    }

    public static void SetUseStandardReposition(DependencyObject obj, bool value)
    {
        obj.SetValue(UseStandardRepositionProperty, value);
    }

    public static readonly DependencyProperty UseStandardRepositionProperty =
        DependencyProperty.RegisterAttached("UseStandardReposition", typeof(bool), typeof(Properties), new PropertyMetadata(false, (d, e) =>
        {
            if (d is FrameworkElement f && e.NewValue is bool b)
            {
                if (b)
                    CompositionFactory.SetStandardReposition(f);
                else
                    CompositionFactory.DisableStandardReposition(f);
            }
        }));

    #endregion

    #region ItemContainerTransitions

    public static TransitionCollection GetItemContainerTransitions(DependencyObject obj)
    {
        return (TransitionCollection)obj.GetValue(ItemContainerTransitionsProperty);
    }

    public static void SetItemContainerTransitions(DependencyObject obj, TransitionCollection value)
    {
        obj.SetValue(ItemContainerTransitionsProperty, value);
    }

    public static readonly DependencyProperty ItemContainerTransitionsProperty =
        DependencyProperty.RegisterAttached("ItemContainerTransitions", typeof(TransitionCollection), typeof(Properties), new PropertyMetadata(null));

    #endregion

    #region ChildrenTransitions

    public static TransitionCollection GetChildrenTransitions(DependencyObject obj)
    {
        return (TransitionCollection)obj.GetValue(ChildrenTransitionsProperty);
    }

    public static void SetChildrenTransitions(DependencyObject obj, TransitionCollection value)
    {
        obj.SetValue(ChildrenTransitionsProperty, value);
    }

    public static readonly DependencyProperty ChildrenTransitionsProperty =
        DependencyProperty.RegisterAttached("ChildrenTransitions", typeof(TransitionCollection), typeof(Properties), new PropertyMetadata(null));

    #endregion

    #region SupportAnimatedIcon

    public static bool GetSupportAnimatedIcon(DependencyObject obj)
    {
        return (bool)obj.GetValue(SupportAnimatedIconProperty);
    }

    public static void SetSupportAnimatedIcon(DependencyObject obj, bool value)
    {
        obj.SetValue(SupportAnimatedIconProperty, value);
    }

    public static readonly DependencyProperty SupportAnimatedIconProperty =
        DependencyProperty.RegisterAttached("SupportAnimatedIcon", typeof(bool), typeof(Properties), new PropertyMetadata(false, (d, a) =>
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
        }));

    #endregion

    #region AttachedStates

    public static bool GetUseAttachedStates(DependencyObject obj)
    {
        return (bool)obj.GetValue(UseAttachedStatesProperty);
    }

    public static void SetUseAttachedStates(DependencyObject obj, bool value)
    {
        obj.SetValue(UseAttachedStatesProperty, value);
    }

    public static readonly DependencyProperty UseAttachedStatesProperty =
        DependencyProperty.RegisterAttached("UseAttachedStates", typeof(bool), typeof(Properties), new PropertyMetadata(false, (d, a) =>
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
        }));



    public static string GetName(DependencyObject obj)
    {
        return (string)obj.GetValue(NameProperty);
    }

    public static void SetName(DependencyObject obj, string value)
    {
        obj.SetValue(NameProperty, value);
    }

    public static readonly DependencyProperty NameProperty =
        DependencyProperty.RegisterAttached("Name", typeof(string), typeof(Properties), new PropertyMetadata(null));



    #endregion

    #region UseClickAnimation

    public static string GetClickAnimation(DependencyObject obj)
    {
        return (string)obj.GetValue(ClickAnimationProperty);
    }

    public static void SetClickAnimation(DependencyObject obj, string value)
    {
        obj.SetValue(ClickAnimationProperty, value);
    }

    public static readonly DependencyProperty ClickAnimationProperty =
        DependencyProperty.RegisterAttached("ClickAnimation", typeof(string), typeof(Properties), new PropertyMetadata(null, (d, e) =>
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
        }));

    public static Double GetClickAnimationOffset(DependencyObject obj)
    {
        return (Double)obj.GetValue(ClickAnimationOffsetProperty);
    }

    public static void SetClickAnimationOffset(DependencyObject obj, Double value)
    {
        obj.SetValue(ClickAnimationOffsetProperty, value);
    }

    public static readonly DependencyProperty ClickAnimationOffsetProperty =
        DependencyProperty.RegisterAttached("ClickAnimationOffset", typeof(Double), typeof(Properties), new PropertyMetadata(-2d));

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

    public static string GetPointerOverAnimation(DependencyObject obj)
    {
        return (string)obj.GetValue(PointerOverAnimationProperty);
    }

    public static void SetPointerOverAnimation(DependencyObject obj, string value)
    {
        obj.SetValue(PointerOverAnimationProperty, value);
    }

    public static readonly DependencyProperty PointerOverAnimationProperty =
        DependencyProperty.RegisterAttached("PointerOverAnimation", typeof(string), typeof(Properties), new PropertyMetadata(null, (d, e) =>
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
        }));

    public static double GetPointerAnimationOffset(DependencyObject obj)
    {
        return (double)obj.GetValue(PointerAnimationOffsetProperty);
    }

    public static void SetPointerAnimationOffset(DependencyObject obj, double value)
    {
        obj.SetValue(PointerAnimationOffsetProperty, value);
    }

    public static readonly DependencyProperty PointerAnimationOffsetProperty =
        DependencyProperty.RegisterAttached("PointerAnimationOffset", typeof(double), typeof(Properties), new PropertyMetadata(-2d));

    #endregion

    #region PointerPressedAnimation

    public static string GetPointerPressedAnimation(DependencyObject obj)
    {
        return (string)obj.GetValue(PointerPressedAnimationProperty);
    }

    public static void SetPointerPressedAnimation(DependencyObject obj, string value)
    {
        obj.SetValue(PointerPressedAnimationProperty, value);
    }

    public static readonly DependencyProperty PointerPressedAnimationProperty =
        DependencyProperty.RegisterAttached("PointerPressedAnimation", typeof(string), typeof(Properties), new PropertyMetadata(null, (d, e) =>
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
        }));

    #endregion

    #region ItemContainerBackgroundTransition

    private static bool GetSetContainerBackgroundTransition(DependencyObject obj)
    {
        return (bool)obj.GetValue(SetContainerBackgroundTransitionProperty);
    }

    private static void SetSetContainerBackgroundTransition(DependencyObject obj, bool value)
    {
        obj.SetValue(SetContainerBackgroundTransitionProperty, value);
    }

    public static readonly DependencyProperty SetContainerBackgroundTransitionProperty =
        DependencyProperty.RegisterAttached("SetContainerBackgroundTransition", typeof(bool), typeof(Properties), new PropertyMetadata(false));


    public static BrushTransition GetItemContainerBackgroundTransition(DependencyObject obj)
    {
        return (BrushTransition)obj.GetValue(ItemContainerBackgroundTransitionProperty);
    }

    public static void SetItemContainerBackgroundTransition(DependencyObject obj, BrushTransition value)
    {
        obj.SetValue(ItemContainerBackgroundTransitionProperty, value);
    }

    public static readonly DependencyProperty ItemContainerBackgroundTransitionProperty =
        DependencyProperty.RegisterAttached("ItemContainerBackgroundTransition", typeof(BrushTransition), typeof(Properties), new PropertyMetadata(null, (d, e) =>
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
        }));

    #endregion

    #region Uppercase

    public static string GetUppercase(DependencyObject obj)
    {
        return (string)obj.GetValue(UppercaseProperty);
    }

    public static void SetUppercase(DependencyObject obj, string value)
    {
        obj.SetValue(UppercaseProperty, value);
    }

    public static readonly DependencyProperty UppercaseProperty =
        DependencyProperty.RegisterAttached("Uppercase", typeof(string), typeof(Properties), new PropertyMetadata(string.Empty, (d, e) =>
        {
            if (d is TextBlock t && e.NewValue is string s)
                t.Text = (s ?? string.Empty).ToUpper();
        }));

    #endregion

    #region DisableTranslation 

    public static bool GetDisableTranslation(DependencyObject obj)
    {
        return (bool)obj.GetValue(DisableTranslationProperty);
    }

    public static void SetDisableTranslation(DependencyObject obj, bool value)
    {
        obj.SetValue(DisableTranslationProperty, value);
    }

    public static readonly DependencyProperty DisableTranslationProperty =
        DependencyProperty.RegisterAttached("DisableTranslation", typeof(bool), typeof(Properties), new PropertyMetadata(false, (d, e) =>
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
        }));
    #endregion

    #region Footer

    public static object GetFooter(DependencyObject obj)
    {
        return (object)obj.GetValue(FooterProperty);
    }

    public static void SetFooter(DependencyObject obj, object value)
    {
        obj.SetValue(FooterProperty, value);
    }

    public static readonly DependencyProperty FooterProperty =
        DependencyProperty.RegisterAttached("Footer", typeof(object), typeof(Properties), new PropertyMetadata(null));

    #endregion

    #region RenderScale

    public static double GetRenderScale(DependencyObject obj)
    {
        return (double)obj.GetValue(RenderScaleProperty);
    }

    public static void SetRenderScale(DependencyObject obj, double value)
    {
        obj.SetValue(RenderScaleProperty, value);
    }

    // Using a DependencyProperty as the backing store for RenderScale.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty RenderScaleProperty =
        DependencyProperty.RegisterAttached("RenderScale", typeof(double), typeof(Properties), new PropertyMetadata(1d, (d, e) =>
        {
            if (d is FrameworkElement f && e.NewValue is double s)
            {
                var ct = f.GetCompositeTransform();
                ct.ScaleX = ct.ScaleY = s;
            }
        }));

    #endregion

    #region Flyout

    public static FlyoutBase GetFlyout(DependencyObject obj)
    {
        return (FlyoutBase)obj.GetValue(FlyoutProperty);
    }

    public static void SetFlyout(DependencyObject obj, FlyoutBase value)
    {
        obj.SetValue(FlyoutProperty, value);
    }

    public static readonly DependencyProperty FlyoutProperty =
        DependencyProperty.RegisterAttached("Flyout", typeof(FlyoutBase), typeof(Properties), new PropertyMetadata(null, (d, e) =>
        {
            if (d is ButtonBase b)
            {
                b.Click -= ButtonFlyoutClick;
                b.Click += ButtonFlyoutClick;
            }
        }));

    private static void ButtonFlyoutClick(object sender, RoutedEventArgs e)
    {
        if (sender is ButtonBase b && GetFlyout(b) is FlyoutBase fly)
        {
            fly.ShowAt(b);
        }
    }

    #endregion

    #region Rotation

    public static double GetRotation(DependencyObject obj)
    {
        return (double)obj.GetValue(RotationProperty);
    }

    public static void SetRotation(DependencyObject obj, double value)
    {
        obj.SetValue(RotationProperty, value);
    }

    public static readonly DependencyProperty RotationProperty =
        AP<double, Properties>(0d, (d, e) =>
        {
            if (d is FrameworkElement f && f.GetElementVisual() is { } v && e.NewValue is double n)
            {
                v.CenterPoint = new(v.Size / 2f, 0f);
                v.RotationAxis = Vector3.UnitZ;
                v.RotationAngleInDegrees = (float)n;
            }
        });

    public static KeyTime GetRotationTransition(DependencyObject obj)
    {
        return (KeyTime)obj.GetValue(RotationTransitionProperty);
    }

    public static void SetRotationTransition(DependencyObject obj, KeyTime value)
    {
        obj.SetValue(RotationTransitionProperty, value);
    }

    public static readonly DependencyProperty RotationTransitionProperty =
        AP<KeyTime, Properties>(KeyTime.FromTimeSpan(TimeSpan.Zero), (d, e) =>
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
        });

    #endregion


}
