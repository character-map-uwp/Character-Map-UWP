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
