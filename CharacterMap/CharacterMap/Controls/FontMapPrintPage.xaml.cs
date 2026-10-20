using CharacterMap.Core;
using CharacterMapCX;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace CharacterMap.Controls;

public sealed partial class FontMapPrintPage : Page
{
    PrintViewModel PrintModel { get; }
    public bool IsInAppPreview { get; }
    public ObservableCollection<Character> Items { get; } = [];

    public FontMapPrintPage(PrintViewModel printModel, DataTemplate t, bool isAppPreview = false)
    {
        PrintModel = printModel;

        this.InitializeComponent();

        UpdateLazyLoad();
        IsInAppPreview = isAppPreview;

        Update();
    }

    public void Update()
    {
        if (ItemsPanel?.ItemsPanelRoot is ItemsWrapGrid g)
        {
            g.ItemWidth = PrintModel.GlyphSize;
            g.ItemHeight = PrintModel.GlyphSize;
        }
    }

    public static int CalculateGlyphsPerPage(Size safePrintAreaSize, PrintViewModel viewModel)
    {
        if (viewModel.Layout == PrintLayout.Grid)
        {
            double size = viewModel.GlyphSize + 4d + 4d; // 4px is GridViewItem padding, 4px is border-thickness.

            int c = (int)Math.Floor((safePrintAreaSize.Width + 6) / size);
            int r = (int)Math.Floor((safePrintAreaSize.Height) / size);

            return r * c;
        }
        else
        {
            double size = viewModel.GlyphSize;
            int r = (int)Math.Floor((safePrintAreaSize.Height + 1) / size);
            return r;
        }
    }

    private void UpdateLazyLoad()
    {
        if (PrintModel.Layout == PrintLayout.Grid)
        {
            this.UnloadObject(ListLayout);
            this.FindName(nameof(GridLayout));
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

        foreach (Character c in e.Skip((page) * charsPerPage).Take(charsPerPage))
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
        return new(horizontal, vertical, horizontal, vertical);
    }

    private void UpdateGlyphImage(Image img, Character c, float size)
    {
        if (PrintModel.FaceAnalysis?.ActiveFace is not { } face)
            return;

        Color textColor = Foreground is SolidColorBrush scb ? scb.Color : Colors.Black;
        IReadOnlyList<uint> typographyTags = PrintModel.Typography is { Feature: not DWriteTypographyFeatureName.None } info
            ? [(uint)info.Feature]
            : [];

        if (c is GlyphCharacter gc)
            img.Source = DirectWrite.GetGlyphImage(face, (ushort)gc.GlyphIndex, size, textColor, GlyphImageFormat.None);
        else
            img.Source = DirectWrite.GetCharacterImage(face, c.Char, size, textColor, GlyphImageFormat.None, typographyTags);
    }

    private void ListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue && args.ItemContainer is ListViewItem item)
        {
            Character c = (Character)args.Item;
            UpdateListContainer(item, c);
            args.Handled = true;
        }
    }

    private void UpdateListContainer(ListViewItem item, Character c)
    {
        item.Height = PrintModel.GlyphSize;
        Grid g = (Grid)item.ContentTemplateRoot;
        g.ColumnDefinitions[0].Width = new(PrintModel.GlyphSize);

        // 1. Update main glyph image
        if (g.Children[0] is Image img)
        {
            img.Height = img.Width = PrintModel.GlyphSize;
            UpdateGlyphImage(img, c, (float)(PrintModel.GlyphSize / 2d));
        }

        // 2. update Unicode
        TextBlock unicodeId = (TextBlock)((StackPanel)g.Children[1]).Children[0];
        unicodeId.SetVisible(PrintModel.Annotation != GlyphAnnotation.None);
        unicodeId.Text = c.GetAnnotation(PrintModel.Annotation);

        // 3. update description
        TextBlock description = (TextBlock)((StackPanel)g.Children[1]).Children[1];
        try
        {
            description.Text = PrintModel.FaceAnalysis.GetDescription(c);
        }
        catch { }

        // 4. handle borders
        foreach (Rectangle r in g.GetFirstLevelDescendantsOfType<Rectangle>())
            r.SetVisible(PrintModel.ShowBorders);
    }

    private void ItemsPanel_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue && args.ItemContainer is GridViewItem item)
        {
            item.IsTabStop = false;
            item.Width = item.Height = PrintModel.GlyphSize;
            if (PrintModel.ShowBorders)
                item.BorderBrush = ResourceHelper.Get<Brush>("PrintBorderBrush");

            if (item.ContentTemplateRoot is Grid g)
            {
                g.Width = g.Height = PrintModel.GlyphSize;
                Character c = (Character)args.Item;
                if (g.Children[0] is Image img)
                    UpdateGlyphImage(img, c, (float)(PrintModel.GlyphSize / 2d));

                if (g.Children.Count > 1 && g.Children[1] is TextBlock unicodeId)
                {
                    unicodeId.SetVisible(PrintModel.Annotation != GlyphAnnotation.None);
                    unicodeId.Text = c.GetAnnotation(PrintModel.Annotation);
                }
            }

            args.Handled = true;
        }
    }
}
