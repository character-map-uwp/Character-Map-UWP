using CharacterMap.Controls;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Provider;
using CharacterMapCX.Controls;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
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
        /// Applies <see cref="CharacterRenderingOptions"/> to a <see cref="DirectText"/> control
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
            DependencyProperty.RegisterAttached("InsetClip", typeof(Thickness), typeof(Properties), new PropertyMetadata(new Thickness(0d), (d,e) => 
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
            DependencyProperty.RegisterAttached("IsCompact", typeof(bool), typeof(Properties), new PropertyMetadata(false, (d,e) =>
            {
                if (d is TabViewItem item && e.NewValue is bool b)
                {
                    VisualStateManager.GoToState(item, b ? "CollapsedTabState" : "FullTabState", true);

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

                        if (view.GetFirstDescendantOfType<TabViewListView>() is ListView list)
                        {
                            list.ContainerContentChanging -= List_ContainerContentChanging;
                            if (enabled)
                                list.ContainerContentChanging += List_ContainerContentChanging;

                            void List_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
                            {
                                // Manually trigger the animation every time the tab content changes
                                if (args.Item is not null && args.ItemContainer is not null)
                                {
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
            DependencyProperty.RegisterAttached("RequireOpenTab", typeof(bool), typeof(Properties), new PropertyMetadata(false, (d,e) =>
            {
                if (d is TabView tabs)
                {
                    tabs.TabItemsChanged -= TabItemsChanged;
                    tabs.TabItemsChanged += TabItemsChanged;
                }

                static void TabItemsChanged(TabView sender, IVectorChangedEventArgs args)
                {
                    // NOTE: These states conflict with TabViews close button mode, but we
                    //       never change that currently in this app, so we're fine to
                    //       reuse those states for this property instead.
                    var items = sender.GetFirstLevelDescendantsOfType<TabViewItem>().ToList();
                    if (GetRequireOpenTab(sender) && sender.TabItems.Count == 1)
                    {
                        foreach (var item in items)
                            VisualStateManager.GoToState(item, "CloseButtonDisabledState", true);
                    }
                    else
                    {
                        foreach (var item in items)
                            VisualStateManager.GoToState(item, "CloseButtonEnabledState", true);
                    }
                }
            }));

        #endregion
    }
}
