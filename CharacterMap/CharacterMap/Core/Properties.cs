using CharacterMap.Controls;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMapCX.Controls;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation.Collections;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Core.Direct;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Core
{
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
                    CharacterGridView.UpdateTypography(x, p, i);
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
                        d.FontFace = o.Variant.FontFace;
                        d.FontFamily = (FontFamily)XamlBindingHelper.ConvertValue(typeof(FontFamily), o.Variant.Source);
                        d.FontStretch = o.Variant.FontFace.Stretch;
                        d.FontStyle = o.Variant.FontFace.Style;
                        d.FontWeight = o.Variant.FontFace.Weight;
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
                        t.FontStretch = o.Variant.FontFace.Stretch;
                        t.FontStyle = o.Variant.FontFace.Style;
                        t.FontWeight = o.Variant.FontFace.Weight;
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

        private static Dictionary<Compositor, CompositionAnimation> _tabAniCache { get; } = new();

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
                                    if (_tabAniCache.TryGetValue(v.Compositor, out CompositionAnimation ani) is false)
                                    {
                                        // Animation doesn't exist, create and cache it
                                        _tabAniCache[v.Compositor] = ani =
                                            v.CreateVector3KeyFrameAnimation(CompositionFactory.TRANSLATION)
                                                .SetDelayBehavior(AnimationDelayBehavior.SetInitialValueBeforeDelay)
                                                .SetDelayTime(0.05)
                                                .AddKeyFrame(0, 0, 60)
                                                .AddKeyFrame(1, 0, 0)
                                                .SetDuration(0.325);
                                    }

                                    v.StartAnimation(ani);
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

                    if (e.NewValue is bool b)
                        f.AreOpenCloseAnimationsEnabled = false;
                }

                static void F_Opened(object sender, object e)
                {
                    if ((sender is FlyoutBase flyout 
                        && flyout.GetPresenter() is FrameworkElement presenter) is false)
                        return;

                    Visual v = presenter.GetElementVisual();

                    // 0. Disable animation if turned off
                    if (GetUseExpandContractAnimation(flyout) is false
                        || ResourceHelper.AllowAnimation is false)
                    {
                        presenter.SetHideAnimation(null);
                        v.Scale = new(1f);
                        return;
                    }

                    // 1. Set scale origin
                    if (presenter.RenderTransformOrigin.X != 0 || presenter.RenderTransformOrigin.Y != 0)
                        CompositionFactory.StartCentering(
                            v, (float)presenter.RenderTransformOrigin.X, (float)presenter.RenderTransformOrigin.Y);

                    // 2. Create Expand animation and play it
                    var entranceEase = v.GetCached<CubicBezierEasingFunction>("EntraceEase", 
                        () => v.Compositor.CreateEntranceEasingFunction());
                    var popOpen = v.GetCached<KeyFrameAnimation>("UECPopupExpand",
                        () => v.CreateVector3KeyFrameAnimation(nameof(Visual.Scale))
                                .AddKeyFrame(0.0f, "Vector3(Min(0.01, 20.0 / this.Target.Size.X), Min(0.01, 20.0 / this.Target.Size.Y), 1.0)")
                                .AddScaleKeyFrame(1, 1, entranceEase)
                                .SetDuration(0.3));

                    v.StartAnimation(popOpen);

                    // 3. Create Contract animation and schedule it
                    var ease = v.GetCached<CubicBezierEasingFunction>("ExitEase",
                        () => v.Compositor.CreateCubicBezierEasingFunction(0.7f, 0.0f, 1.0f, 0.5f));

                    var popClose = v.GetCached<KeyFrameAnimation>("UECPopupContract",
                        () => v.CreateVector3KeyFrameAnimation(nameof(Visual.Scale))
                                .AddScaleKeyFrame(0, 1)
                                .AddKeyFrame(1.0f, "Vector3(Min(0.01, 20.0 / this.Target.Size.X), Min(0.01, 20.0 / this.Target.Size.Y), 1.0)", ease)
                                .SetDuration(0.2));

                    presenter.SetHideAnimation(popClose);
                }
            }));

        #endregion
    }
}
