//#define DX

using CharacterMapCX.Controls;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using Windows.Foundation.Metadata;
using Windows.System;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Core.Direct;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;


internal class CharacterGridViewTemplateSettings
{
    private TypographyFeatureInfo _typography;
    public TypographyFeatureInfo Typography
    { 
        get => _typography; 
        set
        {
            if (_typography != value)
            {
                _typography = value;
                DWriteTypographyCollection col = new();
                if (value is not null && value.Feature != DWriteTypographyFeatureName.None)
                    col.AddFeature(value.Feature);
                TypographyCollection = col;
            }
        }
    }

    public DWriteTypographyCollection TypographyCollection { get; private set; } = new();

    public FontFamily FontFamily { get; set; }
    public DWriteFontFace FontFace { get; set; }
    public bool ShowColorGlyphs { get; set; }
    public double Size { get; set; } = 24;
    public bool EnableReposition { get; set; }
    public GlyphAnnotation Annotation { get; set; }
}

[DependencyProperty<double>("ItemSize")]
[DependencyProperty<bool>("ShowColorGlyphs")]
[DependencyProperty<bool>("EnableResizeAnimation")]
[DependencyProperty<FontFamily>("ItemFontFamily")]
[DependencyProperty<DWriteFontFace>("ItemFontFace")]
[DependencyProperty<TypographyFeatureInfo>("ItemTypography")]
[DependencyProperty<FaceAnalysisModel>("ItemFaceAnalysis")]
[DependencyProperty<GlyphAnnotation>("ItemAnnotation")]
[AttachedProperty<ItemTooltipData>("ToolTipData")]
[DependencyProperty<bool>("HasSelection")]
[DependencyProperty<bool>("IsSelectionBindingEnabled")]
[DependencyProperty<int>("SelectionCount")]
[DependencyProperty<INotifyCollectionChanged>("BindableSelectedItems")]
public partial class CharacterGridView : GridView
{
    public event EventHandler<Character> ItemDoubleTapped;

    public bool ShowVariationsInToolTips { get; set; }

    public bool ForceFontGlyphs { get; set; }

    #region Dependency Properties

    partial void OnItemSizeChanged(double oldValue, double n) => _templateSettings.Size = n;

    partial void OnItemFontFamilyChanged(FontFamily oldValue, FontFamily n) => _templateSettings.FontFamily = n;

    partial void OnItemFontFaceChanged(DWriteFontFace oldValue, DWriteFontFace n) => _templateSettings.FontFace = n;

    partial void OnItemTypographyChanged(TypographyFeatureInfo oldValue, TypographyFeatureInfo n)
    {
        _templateSettings.Typography = n;
        UpdateTypographies(_templateSettings.TypographyCollection);
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

    private XamlDirect _xamlDirect = null;

    private CharacterGridViewTemplateSettings _templateSettings = null;

    public class ItemTooltipData
    {
        public Character Char { get; set; }
        public FaceAnalysisModel FaceAnalysis { get; set; }
        public GridViewItem Container { get; set; }
    }

    public CharacterGridView()
    {
        _xamlDirect = XamlDirect.GetDefault();
        _templateSettings = new ();

        this.ContainerContentChanging += OnContainerContentChanging;
        this.ChoosingItemContainer += OnChoosingItemContainer;
        this.Loaded += ExtendedListView_Loaded;
        this.Unloaded += ExtendedListView_Unloaded;

        this.RegisterPropertyChangedCallback(ForegroundProperty, OnForegroundChanged);
    }

    private void OnForegroundChanged(DependencyObject sender, DP dp)
    {
        if (sender is CharacterGridView g)
        {
            if (ReadLocalValue(ForegroundProperty) is Brush b)
                UpdateForeground(b);
        }
    }

    private void ExtendedListView_Loaded(object sender, RoutedEventArgs e)
    {
        if (IsSelectionBindingEnabled)
            OnAttached();
    }

    private void ExtendedListView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (IsSelectionBindingEnabled)
            OnDetaching();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RealizeGlyphTarget(SelectorItem itemContainer, FaceAnalysisModel analysis, bool force = false)
    {
        if (itemContainer.ContentTemplateRoot is Grid g)
        {
            // We need to decide whether to render with XAML TextBlock or our FontGlyphs
            // control. There are some memory issues with FontGlyphs I haven't been able
            // to figure out yet, so we prefer TextBlock where we can for now.
            //
            // We need to use FontGlyphs if the font face:
            //   - uses ColrV1 glyphs
            //   - has variable axis
            //
            // XAML TextBlock supports neither of these currently, and FontGlyphs has better
            // variable axis support than the DirectText control for some reason, even though
            // they're built on the same axis creation techniques.

            bool needsGlyphs = force || analysis is { ShouldUseDWriteRendering: true };

            // 1. Unload existing presenter if necessary
            if (!force && g.Children[0] is FrameworkElement f)
            {
                if (f is TextBlock { Name: "Block" } && needsGlyphs)
                    XamlMarkupHelper.UnloadObject(f);
                else if (f is FontGlyphs && !needsGlyphs)
                    XamlMarkupHelper.UnloadObject(f);
            }

            // // 2. Load the appropriate if it does not exist
            //if (g.Children.Count == 1)
                g.FindName(needsGlyphs ? "Glyph" : "Block");
        }
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
            RealizeGlyphTarget(args.ItemContainer, ItemFaceAnalysis, ForceFontGlyphs);
            

            UpdateContainer(item.ContentTemplateRoot, c);
            args.Handled = true;

            item.DataContext = c;
            item.DoubleTapped -= Item_DoubleTapped;
            item.DoubleTapped += Item_DoubleTapped;

            // Set ToolTip
            if (ItemFontFace is not null)
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
                             string txt = data?.FaceAnalysis.Face is not null
                                 ? data.FaceAnalysis.GetDescription(data.Char, allowUnihan: true)
                                 : string.Empty;

                            //string formatLabel = FlyoutHelper.GetGlyphFormatLabel(data.Variant, data.Char);
                            //string mainText = txt ?? data.Char.UnicodeString;
                            //t.Text = string.IsNullOrEmpty(formatLabel) ? mainText : $"{mainText} [{formatLabel}]";
                            
                            t.Text = txt ?? data.Char.UnicodeString;

                            // Manually construct the variations popup
                            if (ShowVariationsInToolTips && TypographyAnalyzer.GetCharacterVariants(data.FaceAnalysis.Face, data.Char) is { Count: > 1 } list)
                            {
                                // Store current template settings - we need to change these to render the variations
                                var size = _templateSettings.Size;
                                var typo = _templateSettings.Typography;
                                var anno = _templateSettings.Annotation;

                                // Set values we will use for the variations
                                _templateSettings.Size = 40;
                                _templateSettings.Annotation = GlyphAnnotation.None;
                                
                                // Brush we will as BG use to show each variation
                                SolidColorBrush bg = new ()  { 
                                    Color = tt.ActualTheme == ElementTheme.Dark ? Colors.White : Colors.Black, 
                                    Opacity =  0.05 
                                };

                                StackPanel s = new();
                                s.Children.Add(t);

                                // Add variation header block
                                s.Children.Add(new TextBlock
                                {
                                    Margin = new Thickness(0, 8, 0, 4),
                                    Text = $"{list.Count} {Localization.Get("TypographyVariationsSelectorRun/Text")}",
                                    Opacity = 0.7
                                });

                                // add variation container panel
                                WrapPanel panel = new() { HorizontalSpacing = 4, VerticalSpacing = 4 };
                                s.Children.Add(panel);

                                bool material = ResourceHelper.AppSettings.ApplicationDesignTheme == 4;

                                // Manually add each character
                                foreach (var variation in list)
                                {
                                    _templateSettings.Typography = variation;

                                    var item = (Grid)this.ItemTemplate.LoadContent();
                                    item.DataContext = data.Char;
                                    item.Background = bg;
                                    if (material is false)
                                        item.CornerRadius = new CornerRadius(4);
                                    else
                                        Properties.SetMaterialCornerStyle(item, MaterialCornerStyle.Default);

                                    panel.Children.Add(item);
                                    UpdateContainer(item, data.Char);
                                }

                                // Add everything to the tooltip
                                tt.Content = s;

                                // Restore template settings
                                _templateSettings.Size = size;
                                _templateSettings.Typography = typo;
                                _templateSettings.Annotation = anno;
                            }
                            else
                            {
                                tt.Content = t;
                            }
                        }
                    };
                    ToolTipService.SetToolTip(item, t);
                }

                CharacterGridView.SetToolTipData(t, new ItemTooltipData { Char = c, Container = item, FaceAnalysis = ItemFaceAnalysis });
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

    private void Item_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (sender is GridViewItem item)
        {
            ItemDoubleTapped?.Invoke(sender, item.DataContext as Character);
        }
    }

    private IEnumerable<GridViewItem> GetActiveContainers()
    {
        // Recycled containers are at -10000 usually
        return this.ItemsPanelRoot?.Children.OfType<GridViewItem>();//.Where(c => c.ActualOffset.X >= -1000);
    }




    #region Bindable Selected Items Handling

    long _itemsSourceToken = 0;

    protected void OnAttached()
    {
        if (BindableSelectedItems == null)
        {
            BindableSelectedItems = new ObservableCollection<object>();
        }
        else if (BindableSelectedItems is IEnumerable<object> list)
        {
            foreach (var item in list.ToList())
            {
                SelectedItems.Add(item);
            }
        }

        _itemsSourceToken = RegisterPropertyChangedCallback(ListViewBase.ItemsSourceProperty, ItemsSourceChanged);

        SelectionChanged -= AssociatedObject_SelectionChanged;
        SelectionChanged += AssociatedObject_SelectionChanged;

        BindableSelectedItems.CollectionChanged -= SelectedItems_CollectionChanged;
        BindableSelectedItems.CollectionChanged += SelectedItems_CollectionChanged;

        UpdateSelection();
    }


    protected void OnDetaching()
    {
        UnregisterPropertyChangedCallback(ListViewBase.ItemsSourceProperty, _itemsSourceToken);
        SelectionChanged -= AssociatedObject_SelectionChanged;

        if (BindableSelectedItems is not null)
            BindableSelectedItems.CollectionChanged -= SelectedItems_CollectionChanged;
    }

    partial void OnBindableSelectedItemsChanged(INotifyCollectionChanged o, INotifyCollectionChanged n)
    {
        if (o is not null)
        {
            o.CollectionChanged -= SelectedItems_CollectionChanged;
        }

        if (this is null)
            return;

        if (n is not null)
        {
            n.CollectionChanged -= SelectedItems_CollectionChanged;

            if (n is IList list)
            {
                List<object> items = [.. list];

                this.Enqueue(() =>
                {
                    if (n != BindableSelectedItems)
                        return;

                    SelectionChanged -= AssociatedObject_SelectionChanged;
                    SelectedItems.Clear();

                    foreach (var item in items)
                        SelectedItems.Add(item);

                    UpdateSelection();
                    SelectionChanged += AssociatedObject_SelectionChanged;
                });
            }


            n.CollectionChanged += SelectedItems_CollectionChanged;
        }
    }

    void ItemsSourceChanged(DependencyObject source, DependencyProperty property)
    {
        if (SelectedItems is IList<object> list)
        {
            foreach (var item in list.ToList())
                list.Remove(item);
        }
    }

    void UpdateSelection()
    {
        HasSelection = SelectedItems.Count > 0;
        SelectionCount = SelectedItems.Count;
    }

    public void ClearSelection()
    {
        SelectedItems.Clear();
    }

    private void SelectedItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        SelectionChanged -= AssociatedObject_SelectionChanged;

        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            foreach (var item in e.NewItems)
            {
                if (!SelectedItems.Contains(item))
                    SelectedItems.Add(item);
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            foreach (var item in e.OldItems)
            {
                if (!SelectedItems.Contains(item))
                    SelectedItems.Remove(item);
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            DeselectRange(new ItemIndexRange(0, int.MaxValue));
        }

        UpdateSelection();
        SelectionChanged += AssociatedObject_SelectionChanged;
    }

    private void AssociatedObject_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BindableSelectedItems is IList list)
        {
            BindableSelectedItems.CollectionChanged -= SelectedItems_CollectionChanged;

            foreach (var item in e.RemovedItems)
                list.Remove(item);

            foreach (var item in e.AddedItems)
                list.Add(item);

            BindableSelectedItems.CollectionChanged += SelectedItems_CollectionChanged;
        }

        UpdateSelection();
    }


    #endregion


    #region Item Template Handling

    public void UpdateFontFace()
    {
        if (Utils.SupportsColrV1 is false || this.GetActiveContainers() is not { } containers)
            return;

        foreach (var c in containers)
        {
            if (c.ContentTemplateRoot is Grid g && g.Children[0] is FontGlyphs f)
                f.FontFace = _templateSettings.FontFace;
        }

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void UpdateContainer(UIElement container, Character c)
    {
        // Perf considerations:
        // 1 - Batch rendering updates by suspending rendering until all properties are set
        // 2 - Use XAML direct to set new properties, rather than through DP's
        // 3 - Access any required data properties from parents through normal properties, 
        //     not DP's - DP access can be order of magnitudes slower.
        // Note : This will be faster via C++ as it avoids all marshaling costs.
        // Note: For more improved performance, do **not** use XAML ItemTemplate.
        //       Create entire template via XamlDirect, and never directly reference the 
        //       WinRT XAML object.

        // Assumed Structure:
        // -- Grid
        //    -- FontGlyphs [---TextBlock---]
        //    -- TextBlock

        XamlBindingHelper.SuspendRendering(container);

        XamlDirectWrapper go = new(container, _xamlDirect);

        go.SetObject(XamlPropertyIndex.FrameworkElement_Tag, c)
          .SetWidth(_templateSettings.Size)
          .SetHeight(_templateSettings.Size);

        IXamlDirectObject cld = _xamlDirect.GetXamlDirectObjectProperty(go.Object, XamlPropertyIndex.Panel_Children);
#if DX
{
        var t = (DirectText)((Grid)item.ContentTemplateRoot).Children[0]; ;
        SetGlyphProperties(t, _templateSettings, c);
}
#else
        {
            XamlDirectWrapper o = _xamlDirect.GetWrapperForChild((Panel)container, 0);
            Brush brush = null;
            if (this.ReadLocalValue(ForegroundProperty) is Brush b)
                brush = b;
            SetGlyphProperties(o, _templateSettings, c, brush);
        }
#endif

        if (_xamlDirect.GetWrapperForCollectionIndex(cld, 1) is { } o2)
        {
            switch (_templateSettings.Annotation)
            {
                case GlyphAnnotation.None:
                    o2.SetVisibility(false);
                    break;
                default:
                    o2.SetString(XamlPropertyIndex.TextBlock_Text, c.GetAnnotation(_templateSettings.Annotation))
                      .SetVisibility(true);
                    break;
            }
        }

        XamlBindingHelper.ResumeRendering(container);
    }

    static Brush brush => field ??= new SolidColorBrush(Colors.Red);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetGlyphProperties(XamlDirectWrapper o, CharacterGridViewTemplateSettings templateSettings, Character c, Brush foreground = null)
    {
        if (o == null || templateSettings.FontFace is null)
            return;

        if (o.Source is FontGlyphs g)
        {
            // No XAML Direct here :')
            g.FontSize = templateSettings.Size / 2d;
            g.FontFace = templateSettings.FontFace;
            g.IsColorFontEnabled = templateSettings.ShowColorGlyphs;
            g.UnicodeString = c.Char;
            g.Typography = templateSettings.TypographyCollection;

            if (foreground != null)
                g.Foreground = foreground;
            // else clear value, we don't care right now.
        }
        else
        {
            o.IsTextBlock = true; // Forces XamlDirect to use TextBlock properties, rather than generic UIElement properties
            o.SetFontSize(templateSettings.Size / 2d)
             .SetFontStretch(templateSettings.FontFace.Properties.Stretch)
             .SetFontStyle(templateSettings.FontFace.Properties.Style)
             .SetFontWeight(templateSettings.FontFace.Properties.Weight)
             .SetFontFamily(templateSettings.FontFamily)
             .SetBoolean(XamlPropertyIndex.TextBlock_IsColorFontEnabled, templateSettings.ShowColorGlyphs)
             .SetString(XamlPropertyIndex.TextBlock_Text, c.Char);

            UpdateTypography(o.X, o.Object, templateSettings.Typography);
        }
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
        //if (xd != null)
        //    xamlDirect.SetBooleanProperty(xd, XamlPropertyIndex.TextBlock_IsColorFontEnabled, value);
        //else
        //    block.IsColorFontEnabled = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UpdateTypography(XamlDirect xamlDirect, IXamlDirectObject o, TypographyFeatureInfo info)
    {
        DWriteTypographyFeatureName f = info == null ? DWriteTypographyFeatureName.None : info.Feature;
        TypographySetter.SetTypography(o, f, xamlDirect);
    }



    private void UpdateForeground(Brush brush)
    {
        if (this.GetActiveContainers() is not { } items || brush is null)
            return;

        foreach (var item in items)
        {
            if (item.ContentTemplateRoot is Panel p)
            {
                XamlDirectWrapper o = _xamlDirect.GetWrapperForChild(p, 0);

                if (o.Source is FontGlyphs g)
                    g.Foreground = brush;
            }
        }
    }

    void UpdateColorsFonts(bool value)
    {
        if (ItemsSource == null || ItemsPanelRoot == null)
            return;

        foreach (GridViewItem item in ItemsPanelRoot.Children.OfType<GridViewItem>())
        {
            if (item.ContentTemplateRoot is Panel p)
            {
                XamlDirectWrapper o = _xamlDirect.GetWrapperForChild(p, 0);

                if (o.Source is FontGlyphs g)
                {
                    g.IsColorFontEnabled = value;
                    //g.InvalidateLayoutAndRender();
                }
                else
                    UpdateColorFont(_xamlDirect, null, o.Object, value);
            }
        }
    }

    void UpdateTypographies(DWriteTypographyCollection info)
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
                //if (_xamlDirect.GetXamlDirectObject(item.ContentTemplateRoot) is IXamlDirectObject root)
                //{
                //    var childs = _xamlDirect.GetXamlDirectObjectProperty(root, XamlPropertyIndex.Panel_Children);
                //    IXamlDirectObject tb = _xamlDirect.GetXamlDirectObjectFromCollectionAt(childs, 0);
                //    UpdateTypography(_xamlDirect, tb, info);
                //}

                if (item.ContentTemplateRoot is Panel p)
                {
                    XamlDirectWrapper o = _xamlDirect.GetWrapperForChild(p, 0);

                    if (o.Source is FontGlyphs g)
                    {
                        g.Typography = info;
                        g.InvalidateLayoutAndRender();
                    }
                    else
                        UpdateTypography(_xamlDirect, o.Object, _templateSettings.Typography);
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
            if (_xamlDirect.GetWrapper((Panel)item.ContentTemplateRoot) is { } wrapper)
            {
                wrapper.SetWidth(value)
                       .SetHeight(value)
                       .GetChild(0)
                           .SetFontSize(value / 2d);
            }
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