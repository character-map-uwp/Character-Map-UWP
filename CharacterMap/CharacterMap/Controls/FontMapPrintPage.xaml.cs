using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Core.Direct;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace CharacterMap.Controls;

public sealed partial class FontMapPrintPage : Page
{
    PrintViewModel PrintModel { get; }
    public bool IsInAppPreview { get; }
    public ObservableCollection<Character> Items { get; } = [];


    private DataTemplate _gridTemplate { get; } = null;
    private XamlDirect _xamlDirect { get; } = XamlDirect.GetDefault();

    public FontMapPrintPage(PrintViewModel printModel, DataTemplate t, bool isAppPreview = false)
    {
        PrintModel = printModel;
        _gridTemplate = t;

        this.InitializeComponent();

        UpdateLazyLoad();
        IsInAppPreview = isAppPreview;

        Update();
    }

    public void Update()
    {
        if (ItemsPanel != null)
        {
            ItemsPanel.UpdateSize(PrintModel.GlyphSize);
        }
    }

    public static int CalculateGlyphsPerPage(Size safePrintAreaSize, PrintViewModel viewModel)
    {
        if (viewModel.Layout == PrintLayout.Grid)
        {
            double size = viewModel.GlyphSize + 4d + 4d; // 4px is GridViewItem padding, 4px is border-thickness.

            var c = (int)Math.Floor((safePrintAreaSize.Width + 6) / size);
            var r = (int)Math.Floor((safePrintAreaSize.Height) / size);

            return r * c;
        }
        else
        {
            double size = viewModel.GlyphSize;
            var r = (int)Math.Floor((safePrintAreaSize.Height + 1) / size);
            return r;
        }
    }

    private void UpdateLazyLoad()
    {
        if (PrintModel.Layout == PrintLayout.Grid)
        {
            this.UnloadObject(ListLayout);
            this.FindName(nameof(GridLayout));
            ItemsPanel.ItemTemplate = _gridTemplate;
            ItemsPanel.EnableResizeAnimation = false;
            ItemsPanel.ItemFontFace = PrintModel.Font.Face;
            ItemsPanel.ItemFontFamily = PrintModel.FontFamily;
            ItemsPanel.ItemTypography = PrintModel.Typography;
            ItemsPanel.ShowColorGlyphs = PrintModel.ShowColorGlyphs;
            ItemsPanel.ItemAnnotation = PrintModel.Annotation;
        }
        else if (PrintModel.Layout == PrintLayout.List)
        {
            this.UnloadObject(GridLayout);
            this.FindName(nameof(ListLayout));
        }
        else if (PrintModel.Layout == PrintLayout.TwoColumn)
        {
            this.UnloadObject(GridLayout);
            this.UnloadObject(ListLayout);
        }
    }

    public bool AddCharacters(int page, int charsPerPage, IReadOnlyCollection<Character> e)
    {
        UpdateLazyLoad();

        foreach (var c in e.Skip((page) * charsPerPage).Take(charsPerPage))
            Items.Add(c);

        // Are there still more characters in the font to add?
        return e.Count > (page + 1) * charsPerPage;
    }

    public void ClearCharacters()
    {
        Items.Clear();
    }

    private Thickness GetMargin(double horizontal, double vertical)
    {
        return new Thickness(horizontal, vertical, horizontal, vertical);
    }

    private void ListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue && args.ItemContainer is ListViewItem item)
        {
            Character c = ((Character)args.Item);
            UpdateListContainer(item, c);
            args.Handled = true;
        }
    }

    private void UpdateListContainer(ListViewItem item, Character c)
    {
        item.Height = PrintModel.GlyphSize;
        Grid g = (Grid)item.ContentTemplateRoot;
        g.ColumnDefinitions[0].Width = new GridLength(PrintModel.GlyphSize);

        TextBlock t = (TextBlock)g.Children[0];
        t.Height = t.Width = PrintModel.GlyphSize;

        TextBlock unicodeId = ((TextBlock)((StackPanel)g.Children[1]).Children[0]);
        unicodeId.SetVisible(PrintModel.Annotation != GlyphAnnotation.None);
        unicodeId.Text = c.GetAnnotation(PrintModel.Annotation);

        TextBlock description = ((TextBlock)((StackPanel)g.Children[1]).Children[1]);
        try
        {
            description.Text = PrintModel.Font.GetDescription(c);
        }
        catch { }

        IXamlDirectObject o = _xamlDirect.GetXamlDirectObject(t);
        CharacterGridView.SetGlyphProperties(_xamlDirect, o, PrintModel.GetTemplateSettings(), c);

        foreach (var r in g.GetFirstLevelDescendantsOfType<Rectangle>())
            r.SetVisible(PrintModel.ShowBorders);
    }

    private void ItemsPanel_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue && args.ItemContainer is GridViewItem item)
        {
            item.IsTabStop = false;
            if (PrintModel.ShowBorders)
            {
                item.BorderBrush = ResourceHelper.Get<Brush>("PrintBorderBrush");
            }
        }
    }
}
