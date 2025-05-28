//#define DX

using CharacterMapCX.Controls;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System.Collections.Generic;
using Windows.Foundation.Metadata;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Core.Direct;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;

public record CharacterGridViewTemplateSettings
{
    public FontFamily FontFamily { get; set; }
    public DWriteFontFace FontFace { get; set; }
    public TypographyFeatureInfo Typography { get; set; }
    public bool ShowColorGlyphs { get; set; }
    public double Size { get; set; }
    public bool EnableReposition { get; set; }
    public GlyphAnnotation Annotation { get; set; }
}

[DependencyProperty<double>("ItemSize")]
[DependencyProperty<bool>("ShowColorGlyphs")]
[DependencyProperty<bool>("EnableResizeAnimation")]
[DependencyProperty<FontFamily>("ItemFontFamily")]
[DependencyProperty<DWriteFontFace>("ItemFontFace")]
[DependencyProperty<TypographyFeatureInfo>("ItemTypography")]
[DependencyProperty<CharacterRenderingOptions>("Options")]
[DependencyProperty<FontVariant>("ItemFontVariant")]
[DependencyProperty<GlyphAnnotation>("ItemAnnotation")]
[DependencyProperty<DataTemplate>("VariationTemplate")]
[AttachedProperty<ItemTooltipData>("ToolTipData")]
public partial class CharacterGridView : GridView
{
    public event EventHandler<Character> ItemDoubleTapped;

    public bool ShowVariationsInToolTips { get; set; }

    #region Dependency Properties

    partial void OnItemSizeChanged(double oldValue, double n) => _templateSettings.Size = n;

    partial void OnItemFontFamilyChanged(FontFamily oldValue, FontFamily n) => _templateSettings.FontFamily = n;

    partial void OnItemFontFaceChanged(DWriteFontFace oldValue, DWriteFontFace n) => _templateSettings.FontFace = n;

    partial void OnItemTypographyChanged(TypographyFeatureInfo oldValue, TypographyFeatureInfo n)
    {
        if (n is not null)
        {
            _templateSettings.Typography = n;
            UpdateTypographies(n);
        }
    }

    partial void OnShowColorGlyphsChanged(bool oldValue, bool n)
    {
        _templateSettings.ShowColorGlyphs = n;
        UpdateColorsFonts(n);
    }

    partial void OnEnableResizeAnimationChanged(bool oldValue, bool n)
    {
        _templateSettings.EnableReposition = n && CompositionFactory.UISettings.AnimationsEnabled;
        UpdateAnimation(n);
    }

    partial void OnItemAnnotationChanged(GlyphAnnotation o, GlyphAnnotation n)
    {
        _templateSettings.Annotation = n;
        UpdateUnicode(n);
    }

    #endregion

    public static XamlDirect _xamlDirect = null;

    private CharacterGridViewTemplateSettings _templateSettings = null;

    public CharacterGridView()
    {
        _xamlDirect ??= XamlDirect.GetDefault();
        _templateSettings = new ();

        this.ContainerContentChanging += OnContainerContentChanging;
        this.ChoosingItemContainer += OnChoosingItemContainer;
    }

    public class ItemTooltipData
    {
        public Character Char { get; set; }
        public FontVariant Variant { get; set; }
        public GridViewItem Container { get; set; }
    }

    private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        /* 
         * For performance reasons, we've forgone XAML bindings and
         * will update everything in code 
         */
        if (!args.InRecycleQueue && args.ItemContainer is GridViewItem item)
        {
            Character c = ((Character)args.Item);
            UpdateContainer(item.ContentTemplateRoot, c, ref _templateSettings);
            args.Handled = true;

            item.DataContext = c;
            item.DoubleTapped -= Item_DoubleTapped;
            item.DoubleTapped += Item_DoubleTapped;

            // Set ToolTip
            if (ItemFontVariant is not null)
            {
                if ((ToolTipService.GetToolTip(item) is ToolTip t) is false)
                {
                    t = new();
                    t.PlacementTarget = item;
                    t.VerticalOffset = 4;
                    t.Placement = Windows.UI.Xaml.Controls.Primitives.PlacementMode.Top;
                    if (ResourceHelper.TryGet("DefaultThemeToolTipStyle", out Style style))
                        t.Style = style;

                    t.Loaded += (d, e) =>
                    {
                        if (d is ToolTip tt && CharacterGridView.GetToolTipData(tt) is ItemTooltipData data)
                        {
                            tt.PlacementRect = new(0, 0, data.Container.ActualWidth, data.Container.ActualHeight);

                            // Do not use object initializer Constructor here, this will result in random NullReferenceExceptions.
                            // No idea why.
                            TextBlock t = new();
                            t.TextWrapping = TextWrapping.Wrap;
                            string txt = data.Variant is not null
                                ? data.Variant.GetDescription(data.Char, allowUnihan: true)
                                : string.Empty;
                            t.Text = txt ?? data.Char.UnicodeString;

                            // Manually construct the variations popup
                            if (ShowVariationsInToolTips && TypographyAnalyzer.GetCharacterVariants(data.Variant, data.Char) is { Count: > 1 } list)
                            {
                                // Store current template settings - we need to change these to render the variations
                                var og = _templateSettings;
                                
                                // Set values we will use for the variations
                                _templateSettings = _templateSettings with { Size = 40, Annotation = GlyphAnnotation.None };

                                StackPanel s =
                                    BuildVariationsPanel(
                                        data.Char, 
                                        this.VariationTemplate, 
                                        list, 
                                        Options,
                                        tt.ActualTheme,
                                        ref _templateSettings);

                                // inject header
                                s.Children.Insert(0, t);

                                tt.Content = s;

                                // Restore template settings
                                _templateSettings = og;
                            }
                            else
                            {
                                tt.Content = t;
                            }
                        }
                    };

                    t.Unloaded += (d, e) =>
                    {
                        if (d is ToolTip tt)
                            tt.Content = null;
                    };

                    //LeakTrackingService.Register(t);
                    ToolTipService.SetToolTip(item, t);
                }

                CharacterGridView.SetToolTipData(t, new ItemTooltipData { Char = c, Container = item, Variant = ItemFontVariant });
            }
        }

        if (_templateSettings.EnableReposition)
        {
            if (args.InRecycleQueue)
            {
                PokeUIElementZIndex(args.ItemContainer);
            }
            else
            {
                var v = ElementCompositionPreview.GetElementVisual(args.ItemContainer);
                v.ImplicitAnimations = CompositionFactory.GetRepositionCollection(v.Compositor);
            }
        }
    }

    public static StackPanel BuildVariationsPanel(
        Character c,
        DataTemplate template,
        List<TypographyFeatureInfo> list,
        CharacterRenderingOptions options,
        ElementTheme theme,
        ref CharacterGridViewTemplateSettings settings)
    {
        // Brush we will as BG use to show each variation
        SolidColorBrush bg = new()
        {
            Color = theme == ElementTheme.Dark ? Colors.White : Colors.Black,
            Opacity = 0.05
        };

        StackPanel s = new();

        // Add variation header block
        s.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 8, 0, 4),
            Text = $"{list.Count} {Localization.Get("TypographyVariationsSelectorRun/Text")}",
            Opacity = 0.7
        });

        WrapPanel panel = new() { HorizontalSpacing = 4, VerticalSpacing = 4 };
        s.Children.Add(panel);

        bool material = ResourceHelper.AppSettings.ApplicationDesignTheme == 4;

        // Manually add each character
        foreach (var variation in list)
        {
            options = options with { Typography = [variation] };

            // Create container
            Border item = new() { Height = 40, Width = 40 };
            item.DataContext = c;
            item.Background = bg;

            // Load content
            var content = (DirectText)template.LoadContent();
            content.Margin = new Thickness(8);
            content.Loaded += Content_Loaded;
            Core.Properties.SetOptions(content, options);
            content.FontSize = 12;
            item.Child = content;

            // Give it some styleeee
            if (material is false)
                item.CornerRadius = new CornerRadius(4);
            else
                Properties.SetMaterialCornerStyle(item, MaterialCornerStyle.Default);

            panel.Children.Add(item);
        }

        return s;
    }

    private static void Content_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is DirectText t)
        {
            t.Loaded -= Content_Loaded;
            LeakTrackingService.Register(t);
            LeakTrackingService.Register(t.InternalCanvas);
        }
    }

    private static void T_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is DirectText t)
            t.Unloaded -= T_Unloaded;
    }

    private void Item_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (sender is GridViewItem item)
        {
            ItemDoubleTapped?.Invoke(sender, item.DataContext as Character);
        }
    }




    #region Item Template Handling

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void UpdateContainer(
        UIElement item, 
        Character c, 
        ref CharacterGridViewTemplateSettings _templateSettings)
    {
        // Perf considerations:
        // 1 - Batch rendering updates by suspending rendering until all properties are set
        // 2 - Use XAML direct to set new properties, rather than through DP's
        // 3 - Access any required data properties from parents through normal properties, 
        //     not DP's - DP access can be order of magnitudes slower.
        // Note : This will be faster via C++ as it avoids all marshalling costs.
        // Note: For more improved performance, do **not** use XAML ItemTemplate.
        //       Create entire template via XamlDirect, and never directly reference the 
        //       WinRT XAML object.

        // Assumed Structure:
        // -- Grid
        //    -- TextBlock
        //    -- TextBlock

        XamlBindingHelper.SuspendRendering(item);

        IXamlDirectObject go = _xamlDirect.GetXamlDirectObject(item);

        _xamlDirect.SetObjectProperty(go, XamlPropertyIndex.FrameworkElement_Tag, c);
        _xamlDirect.SetDoubleProperty(go, XamlPropertyIndex.FrameworkElement_Width, _templateSettings.Size);
        _xamlDirect.SetDoubleProperty(go, XamlPropertyIndex.FrameworkElement_Height, _templateSettings.Size);

        IXamlDirectObject cld = _xamlDirect.GetXamlDirectObjectProperty(go, XamlPropertyIndex.Panel_Children);
#if DX
{
        var t = (DirectText)((Grid)item.ContentTemplateRoot).Children[0]; ;
        SetGlyphProperties(t, _templateSettings, c);
}
#else
        {
            IXamlDirectObject o = _xamlDirect.GetXamlDirectObjectFromCollectionAt(cld, 0);
            SetGlyphProperties(_xamlDirect, o, _templateSettings, c);
        }
#endif

        IXamlDirectObject o2 = _xamlDirect.GetXamlDirectObjectFromCollectionAt(cld, 1);
        if (o2 != null)
        {
            switch (_templateSettings.Annotation)
            {
                case GlyphAnnotation.None:
                    _xamlDirect.SetEnumProperty(o2, XamlPropertyIndex.UIElement_Visibility, 1);
                    break;
                default:
                    _xamlDirect.SetStringProperty(o2, XamlPropertyIndex.TextBlock_Text, c.GetAnnotation(_templateSettings.Annotation));
                    _xamlDirect.SetEnumProperty(o2, XamlPropertyIndex.UIElement_Visibility, 0);
                    break;
            }
        }

        XamlBindingHelper.ResumeRendering(item);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetGlyphProperties(XamlDirect xamlDirect, IXamlDirectObject o, CharacterGridViewTemplateSettings templateSettings, Character c)
    {
        if (o == null || templateSettings.FontFace is null)
            return;

        xamlDirect.SetObjectProperty(o, XamlPropertyIndex.TextBlock_FontFamily, templateSettings.FontFamily);
        xamlDirect.SetEnumProperty(o, XamlPropertyIndex.TextBlock_FontStretch, (uint)templateSettings.FontFace.Properties.Stretch);
        xamlDirect.SetEnumProperty(o, XamlPropertyIndex.TextBlock_FontStyle, (uint)templateSettings.FontFace.Properties.Style);
        xamlDirect.SetObjectProperty(o, XamlPropertyIndex.TextBlock_FontWeight, templateSettings.FontFace.Properties.Weight);
        xamlDirect.SetBooleanProperty(o, XamlPropertyIndex.TextBlock_IsColorFontEnabled, templateSettings.ShowColorGlyphs);
        xamlDirect.SetDoubleProperty(o, XamlPropertyIndex.TextBlock_FontSize, templateSettings.Size / 2d);

        UpdateColorFont(xamlDirect, null, o, templateSettings.ShowColorGlyphs);
        UpdateTypography(xamlDirect, o, templateSettings.Typography);

        xamlDirect.SetStringProperty(o, XamlPropertyIndex.TextBlock_Text, c.Char);
    }

    internal static void SetGlyphProperties(DirectText o, CharacterGridViewTemplateSettings templateSettings, Character c)
    {
        if (o == null)
            return;

        o.FontFamily = templateSettings.FontFamily;
        o.FontFace = templateSettings.FontFace;
        o.FontStretch = templateSettings.FontFace.Properties.Stretch;
        o.FontStyle = templateSettings.FontFace.Properties.Style;
        o.FontWeight = templateSettings.FontFace.Properties.Weight;
        o.IsColorFontEnabled = templateSettings.ShowColorGlyphs;
        o.FontSize = templateSettings.Size / 2d;
        o.Typography = templateSettings.Typography;

        o.Text = c.Char;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void UpdateColorFont(XamlDirect xamlDirect, TextBlock block, IXamlDirectObject xd, bool value)
    {
        if (xd != null)
            xamlDirect.SetBooleanProperty(xd, XamlPropertyIndex.TextBlock_IsColorFontEnabled, value);
        else
            block.IsColorFontEnabled = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UpdateTypography(XamlDirect xamlDirect, IXamlDirectObject o, TypographyFeatureInfo info)
    {
        CanvasTypographyFeatureName f = info == null ? CanvasTypographyFeatureName.None : info.Feature;
        TypographyBehavior.SetTypography(o, f, xamlDirect);
    }

    void UpdateColorsFonts(bool value)
    {
        if (ItemsSource == null || ItemsPanelRoot == null)
            return;

        foreach (GridViewItem item in ItemsPanelRoot.Children.OfType<GridViewItem>())
        {
            if (_xamlDirect.GetXamlDirectObject(item.ContentTemplateRoot) is IXamlDirectObject root)
            {
                var childs = _xamlDirect.GetXamlDirectObjectProperty(root, XamlPropertyIndex.Panel_Children);
                IXamlDirectObject tb = _xamlDirect.GetXamlDirectObjectFromCollectionAt(childs, 0);
                UpdateColorFont(_xamlDirect, null, tb, value);
            }
        }
    }

    void UpdateTypographies(TypographyFeatureInfo info)
    {
        if (ItemsSource == null || ItemsPanelRoot == null)
            return;

        foreach (GridViewItem item in ItemsPanelRoot.Children.OfType<GridViewItem>())
        {
#if DX
{
            if (item.ContentTemplateRoot is Grid g)
            {
                DirectText tb = (DirectText)g.Children[0];
                tb.Typography = info;
            }
}
#else
            {
                if (_xamlDirect.GetXamlDirectObject(item.ContentTemplateRoot) is IXamlDirectObject root)
                {
                    var childs = _xamlDirect.GetXamlDirectObjectProperty(root, XamlPropertyIndex.Panel_Children);
                    IXamlDirectObject tb = _xamlDirect.GetXamlDirectObjectFromCollectionAt(childs, 0);
                    UpdateTypography(_xamlDirect, tb, info);
                }
            }
#endif
        }
    }

    void UpdateUnicode(GlyphAnnotation value)
    {
        if (ItemsSource == null || ItemsPanelRoot == null)
            return;

        foreach (GridViewItem item in ItemsPanelRoot.Children.OfType<GridViewItem>())
        {
            if (_xamlDirect.GetXamlDirectObject(item.ContentTemplateRoot) is IXamlDirectObject root)
            {
                if (_xamlDirect.GetObjectProperty(root, XamlPropertyIndex.FrameworkElement_Tag) is Character c)
                {
                    var childs = _xamlDirect.GetXamlDirectObjectProperty(root, XamlPropertyIndex.Panel_Children);
                    IXamlDirectObject tb = _xamlDirect.GetXamlDirectObjectFromCollectionAt(childs, 1);
                    _xamlDirect.SetStringProperty(tb, XamlPropertyIndex.TextBlock_Text, c.GetAnnotation(value));
                    _xamlDirect.SetEnumProperty(tb, XamlPropertyIndex.UIElement_Visibility, (uint)(value != GlyphAnnotation.None ? 0 : 1));
                }
            }

            //if (item.ContentTemplateRoot is Grid g)
            //{
            //    if (g.Tag is Character c)
            //    {
            //        TextBlock tb = (TextBlock)g.Children[1];
            //        tb.Text = c.GetAnnotation(value);
            //        tb.SetVisible(value != GlyphAnnotation.None);
            //    }
            //}
        }
    }

    public void UpdateSize(double value)
    {
        ItemSize = value;
        if (this.Items.Count == 0 || ItemsPanelRoot == null)
            return;

        foreach (GridViewItem item in ItemsPanelRoot.Children.OfType<GridViewItem>())
        {
            if (_xamlDirect.GetXamlDirectObject(item.ContentTemplateRoot) is IXamlDirectObject root)
            {
                _xamlDirect.SetDoubleProperty(root, XamlPropertyIndex.FrameworkElement_Width, value);
                _xamlDirect.SetDoubleProperty(root, XamlPropertyIndex.FrameworkElement_Height, value);
                var childs = _xamlDirect.GetXamlDirectObjectProperty(root, XamlPropertyIndex.Panel_Children);
                IXamlDirectObject tb = _xamlDirect.GetXamlDirectObjectFromCollectionAt(childs, 0);
                _xamlDirect.SetDoubleProperty(tb, XamlPropertyIndex.Control_FontSize, value / 2d);
            }

            //if (item.ContentTemplateRoot is Grid g)
            //{
            //    g.Width = value;
            //    g.Height = value;
            //    ((TextBlock)g.Children[0]).FontSize = value / 2d;
            //}
        }
    }

    #endregion


    #region Reposition Animation

    private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
    {
        if (_templateSettings.EnableReposition && args.ItemContainer != null)
        {
            PokeUIElementZIndex(args.ItemContainer);
        }
    }

    private void UpdateAnimation(bool newValue)
    {
        if (this.ItemsPanelRoot == null)
            return;

        foreach (var item in this.ItemsPanelRoot.Children)
        {
            var v = ElementCompositionPreview.GetElementVisual(item);
            v.ImplicitAnimations = newValue ? CompositionFactory.GetRepositionCollection(v.Compositor) : null;
        }
    }

    private void PokeUIElementZIndex(UIElement e)
    {
        CompositionFactory.PokeUIElementZIndex(e, _xamlDirect);
    }

    #endregion
}