using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using CharacterMap.Wpf.Controls;
using CharacterMap.Wpf.Models;
using CharacterMap.Wpf.Services;
using CharacterMap.Wpf.ViewModels;

internal static class Program
{
    private static int _checks;
    [STAThread]
    private static int Main()
    {
        try
        {
            var app = new Application();
            app.Resources["BooleanToVisibilityConverter"] = new BooleanToVisibilityConverter();
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/CharacterMap.Wpf;component/Themes/Controls.xaml", UriKind.Relative) });
            Check(UnicodeDataService.GetName(0x31) == "DIGIT ONE", "shared Unicode names");
            Check(UnicodeDataService.GetName(0xAC00) == "HANGUL SYLLABLE GA", "algorithmic Hangul names");
            Check(UnicodeDataService.GetName(0x4E2D) == "CJK UNIFIED IDEOGRAPH-4E2D", "CJK range names");
            Check(UnicodeDataService.GetBlock(0x1F600).Name == "Emoticons", "supplementary-plane block");
            Check(MainWindowViewModel.ParseCodePoint("1") == 0x31, "literal digit search");
            Check(MainWindowViewModel.ParseCodePoint("U+0031") == 0x31, "explicit hex search");
            Check(MainWindowViewModel.ParseCodePoint("1F600") == 0x1F600, "supplementary hex search");
            Check(MainWindowViewModel.ParseCodePoint("😀") == 0x1F600, "supplementary literal search");
            Check(MainWindowViewModel.ParseCodePoint("U+D800") == null, "reject surrogate scalar");
            var vm = new MainWindowViewModel();
            Pump(vm.InitializeAsync());
            Check(vm.AllFonts.Count > 0 && vm.CurrentFace != null && vm.Glyphs.Count > 0, "font catalog initialization");
            int fontCount = vm.AllFonts.Count;
            foreach (var font in vm.AllFonts.Where(f => f.DisplayName is "Segoe Fluent Icons" or "Segoe MDL2 Assets"))
                Check(font.DisplayFamily.Source == "Segoe UI", $"{font.DisplayName} has a readable list name");
            var latin = vm.AllFonts.First(f => f.DisplayName == "Segoe UI");
            Check(ReferenceEquals(latin.DisplayFamily, latin.Family), "supported font names retain their own preview font");
            var missingName = new FontEntry("Missing\U0010FFFF", latin.Family, latin.GlyphTypeface);
            Check(!ReferenceEquals(missingName.DisplayFamily, missingName.Family), "unsupported name characters use UI fallback");
            var icon = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/CharacterMap.Wpf;component/Assets/CharacterMap.ico"));
            using (var iconStream = icon.Stream)
            {
                var frames = new IconBitmapDecoder(iconStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames;
                Check(new[] { 16, 24, 32, 48, 256 }.All(size => frames.Any(frame => frame.PixelWidth == size)), "application icon includes native shell and high-DPI sizes");
            }
            Pump(vm.InitializeAsync());
            Check(vm.AllFonts.Count == fontCount, "repeated Loaded events do not reload or duplicate fonts");
            var uncached = new FontEntry("Test", vm.SelectedFont!.Family, vm.CurrentFace!);
            int identity = uncached.GetHashCode(); _ = uncached.Variants;
            Check(identity == uncached.GetHashCode(), "font selector identity stays stable when variant cache is populated");
            vm.SelectedFont = vm.AllFonts.First(f => f.DisplayName == "Segoe UI");
            vm.CodePointSearch = "digit one"; vm.ApplyGlyphFilter();
            Check(vm.Glyphs.Any(g => g.CodePoint == 0x31), "Unicode name search");
            vm.CodePointSearch = "U+0031"; vm.ApplyGlyphFilter();
            Check(vm.Glyphs.Count == 1 && vm.SelectedGlyph?.Text == "1", "code-point filtering");
            vm.AddCommand.Execute(null); Check(vm.ComposerText == "1", "character composition");
            var first = vm.SelectedTab!;
            vm.AddTab(); vm.CodePointSearch = "U+0042"; vm.ApplyGlyphFilter(); vm.ComposerText = "second";
            var second = vm.SelectedTab!;
            vm.SelectedTab = first;
            Check(vm.ComposerText == "1" && vm.SelectedGlyph?.CodePoint == 0x31, "tab restores search, selection and composer");
            vm.SelectedTab = second;
            Check(vm.ComposerText == "second" && vm.SelectedGlyph?.CodePoint == 0x42, "second tab remains independent");
            vm.CloseTab(second); Check(vm.Tabs.Count == 1 && vm.SelectedTab == first, "close selected tab");
            vm.CloseTab(first); Check(vm.Tabs.Count == 1, "last tab stays available");
            vm.CodePointSearch = ""; vm.ApplyGlyphFilter();
            vm.SelectedBlock = vm.Blocks.First(b => b.Name == "Basic Latin");
            Check(vm.Glyphs.All(g => g.CodePoint <= 0x7F), "Unicode block filter");
            vm.SelectedBlock = vm.Blocks[0];
            var regular = vm.CurrentFace;
            vm.SelectedVariant = vm.Variants.First(v => v.Weight == FontWeights.Bold && v.Style == FontStyles.Normal);
            Check(vm.CurrentFace != regular && vm.CurrentFace!.Weight == FontWeights.Bold, "actual bold font face");
            var entry = new FontCatalogService().LoadFontCollection(vm.CurrentFace!.FontUri.LocalPath);
            Check(entry.Count > 0 && entry[0].Variants.Count > 0, "local font file import");
            AdvancedFontTests.Run(vm, Check);
            TestColorPreview(vm);
            TestGrid(vm.CurrentFace);
            TestExport(vm.CurrentFace);
            TestWindowBindings();
            var paginator = new FontMapPaginator(vm.CurrentFace, Enumerable.Range(32, 240).Select(c => new GlyphItem(c)).ToArray(), "Test", new Size(760, 1000));
            Check(paginator.PageCount > 1, "font map print pagination");
            Check(paginator.GetPage(paginator.PageCount - 1) != System.Windows.Documents.DocumentPage.Missing, "last printed page");
            Check(paginator.GetPage(paginator.PageCount) == System.Windows.Documents.DocumentPage.Missing, "print page bounds");
            Console.WriteLine($"PASS: {_checks} checks");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void TestGrid(GlyphTypeface face)
    {
        var grid = new CharacterGridView { Width = 650, Height = 480, ItemSize = 80, ItemFontFace = face, ItemsSource = Enumerable.Range(32, 50000).Select(c => new GlyphItem(c)).ToArray() };
        grid.Style = (Style)Application.Current.FindResource(typeof(CharacterGridView));
        grid.ApplyTemplate();
        Layout(grid);
        var panel = Descendants(grid).OfType<VirtualizingWrapPanel>().Single();
        Check(panel.Columns >= 7, "adaptive grid columns");
        int realized = Descendants(grid).OfType<ListBoxItem>().Count();
        Check(realized > 0 && realized < 100, $"50,000 characters only realize viewport ({realized} containers)");
        panel.ScrollToIndex(49999); Layout(grid);
        Check(grid.ItemContainerGenerator.ContainerFromIndex(49999) != null, "last character can be scrolled into view");
        Check(Descendants(grid).OfType<ListBoxItem>().Count() < 100, "scrolling keeps container count bounded");
        grid.Width = 330; grid.ItemSize = 112; Layout(grid);
        Check(panel.Columns <= 3, "resize recalculates columns");
        grid.ItemsSource = Array.Empty<GlyphItem>(); Layout(grid);
        Check(panel.VerticalOffset == 0 && Descendants(grid).OfType<ListBoxItem>().Count() == 0, "empty filter removes old containers and clamps offset");
        grid.ItemsSource = new[] { new GlyphItem(0x31) }; Layout(grid);
        Check(grid.ItemContainerGenerator.ContainerFromIndex(0) != null, "refill after empty results");
    }
    private static void TestColorPreview(MainWindowViewModel vm)
    {
        var emoji = vm.AllFonts.First(f => f.DisplayName == "Segoe UI Emoji");
        var face = emoji.GlyphTypeface;
        var layers = ColorGlyphService.ForFace(face).GetLayers(face.CharacterToGlyphMap[0x1F600]);
        Check(layers is { Count: > 1 }, "Segoe UI Emoji supplies multiple color layers");
        var fontBytes = File.ReadAllBytes(face.FontUri.LocalPath);
        int U16(int p) => System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(fontBytes.AsSpan(p));
        int U32(int p) => (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(fontBytes.AsSpan(p));
        int colrEntry = Enumerable.Range(0, U16(4)).Select(i => 12 + i * 16).Single(p => U32(p) == 0x434F4C52);
        int colrStart = U32(colrEntry + 8), layerStart = colrStart + U32(colrStart + 8);
        var invalid = (byte[])fontBytes.Clone();
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(invalid.AsSpan(layerStart), ushort.MaxValue);
        bool rejected = false;
        try { ColorGlyphService.Parse(invalid, face.GlyphCount); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "invalid color layer glyph reference is rejected before rendering");
        rejected = false;
        try { ColorGlyphService.Parse(fontBytes[..(colrStart + 8)], face.GlyphCount); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "truncated color table is rejected");
        var glyph = new DirectText { GlyphTypeface = face, CodePoint = 0x1F600, Width = 160, Height = 160, FitToBounds = true, Padding = new Thickness(8), Foreground = Brushes.Black };
        Layout(glyph);
        int ColoredPixels(Visual visual, int width, int height)
        {
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var pixels = new byte[width * height * 4]; bitmap.CopyPixels(pixels, width * 4, 0);
            return Enumerable.Range(0, width * height).Count(i => pixels[i * 4 + 3] > 100 && Math.Abs(pixels[i * 4 + 2] - pixels[i * 4]) > 40);
        }
        Check(ColoredPixels(glyph, 160, 160) > 1000, "emoji renders colored pixels, not monochrome outlines");
        glyph.CodePoint = 0x1F499; Layout(glyph);
        Check(ColoredPixels(glyph, 160, 160) > 1000, "recycled glyph refreshes color layers when code point changes");
        glyph.GlyphTypeface = vm.AllFonts.First(f => f.DisplayName == "Segoe UI").GlyphTypeface; glyph.CodePoint = 0x41; Layout(glyph);
        Check(ColoredPixels(glyph, 160, 160) == 0 && glyph.GetOutline() is { Bounds.IsEmpty: false }, "ordinary fonts retain monochrome outlines after switching face");

        var preview = new FontHoverPreview { Font = emoji, Height = 168 };
        preview.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); Layout(preview);
        Check(preview.Children.Count == 18 && ColoredPixels(preview, 336, 168) > 1000, "hover preview contains eighteen actual colored glyphs");
        GlyphExportService.SavePng(Path.Combine(AppContext.BaseDirectory, "wpf-emoji-hover.png"), preview, 336, 168);
    }
    private static void TestWindowBindings()
    {
        var window = new CharacterMap.Wpf.MainWindow();
        var vm = (MainWindowViewModel)window.DataContext;
        Pump(vm.InitializeAsync());
        var list = (ExtendedListView)window.FindName("FontList");
        var content = (FrameworkElement)window.Content;
        content.Width = 1440; content.Height = 920; Layout(content);
        var font = vm.AllFonts.First(f => f.DisplayName == "Arial");
        list.SetCurrentValue(ListBox.SelectedItemProperty, font); Drain();
        Check(vm.SelectedFont == font, "font list selection updates window view model");
        vm.FontSearch = "Freestyle";
        var script = vm.AllFonts.FirstOrDefault(f => f.DisplayName == "Freestyle Script") ?? font;
        Layout(content);
        var container = (ListBoxItem?)list.ItemContainerGenerator.ContainerFromItem(script);
        Check(container != null, "filtered font row is realized");
        container!.SetCurrentValue(ListBoxItem.IsSelectedProperty, true); Drain();
        Check(vm.SelectedFont == script, "selection still updates after filtering the font list");
        var tip = container.ToolTip as ToolTip;
        Check(tip != null && ToolTipService.GetInitialShowDelay(container) == 500, "font rows expose delayed hover preview");
        tip!.PlacementTarget = container; Drain();
        Check(ReferenceEquals(tip.DataContext, script), "hover tooltip binds to its own font row");
        var hover = Descendants((DependencyObject)tip.Content).OfType<FontHoverPreview>().Single();
        Check(ReferenceEquals(hover.Font, script), "tooltip preview receives hovered font independently of selection");
        var tabs = Descendants(content).OfType<ExtendedTabView>().Single();
        Check(!System.Windows.Shell.WindowChrome.GetIsHitTestVisibleInChrome(tabs), "empty tab strip remains a native caption drag region");
        Check(Descendants(tabs).OfType<ListBoxItem>().All(System.Windows.Shell.WindowChrome.GetIsHitTestVisibleInChrome), "tab items remain interactive in caption");
        var grid = (CharacterGridView)window.FindName("CharacterGrid");
        var glyph = vm.Glyphs.First(g => g.CodePoint == 0x31);
        grid.SetCurrentValue(ListBox.SelectedItemProperty, glyph); Drain();
        Check(vm.SelectedGlyph == glyph, "character grid selection updates preview view model");
        Layout(content);
        ((Grid)content).Background = (Brush)Application.Current.FindResource("PageBrush");
        Layout(content);
        string rendering = Path.Combine(AppContext.BaseDirectory, "wpf-main-render.png");
        GlyphExportService.SavePng(rendering, content, 1440, 920);
        Console.WriteLine("Main window render: " + rendering);
        window.Content = null; // Never run the real window's settings-save handler in tests.
    }
    private static void TestExport(GlyphTypeface face)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CharacterMap-Wpf-Tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string svg = Path.Combine(directory, "glyph.svg"), png = Path.Combine(directory, "glyph.png");
        GlyphExportService.Export(svg, face, new GlyphItem(0x31));
        var doc = XDocument.Load(svg);
        Check(doc.Root?.Name.LocalName == "svg" && doc.Descendants().Any(n => n.Name.LocalName == "path" && n.Attribute("d")?.Value.Length > 10), "SVG contains actual vector outline");
        Check(doc.Descendants().First(n => n.Name.LocalName == "path").Attribute("d")!.Value.StartsWith('M'), "SVG path excludes WPF-only fill-rule prefix");
        GlyphExportService.Export(png, face, new GlyphItem(0x31));
        using var stream = File.OpenRead(png);
        var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        Check(frame.PixelWidth == 1024 && frame.PixelHeight == 1024, "PNG export dimensions");
        byte[] pixels = new byte[1024 * 1024 * 4]; frame.CopyPixels(pixels, 4096, 0);
        Check(pixels.Where((_, i) => i % 4 == 3).Any(a => a > 0) && pixels[3] == 0, "PNG has ink and transparent margins");
        Console.WriteLine($"Export artifacts: {directory}");
    }
    private static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(element.Width, element.Height)); element.Arrange(new Rect(0, 0, element.Width, element.Height)); element.UpdateLayout(); Drain();
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static void Pump(Task task)
    {
        while (!task.IsCompleted) Drain();
        task.GetAwaiter().GetResult();
    }
    private static void Drain()
    {
        var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false); Dispatcher.PushFrame(frame);
    }
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + name);
        _checks++; Console.WriteLine("PASS: " + name);
    }
}
