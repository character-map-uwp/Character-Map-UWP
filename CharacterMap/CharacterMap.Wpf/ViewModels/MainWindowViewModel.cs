using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CharacterMap.Wpf.Infrastructure;
using CharacterMap.Wpf.Models;
using CharacterMap.Wpf.Services;
using Microsoft.Win32;

namespace CharacterMap.Wpf.ViewModels;

public sealed class FontTab : INotifyPropertyChanged
{
    private FontEntry? _font;
    public FontEntry? Font { get => _font; set { _font = value; PropertyChanged?.Invoke(this, new(nameof(Title))); PropertyChanged?.Invoke(this, new(nameof(Font))); } }
    public string Title => Font?.DisplayName ?? "新标签页";
    public FontVariant? Variant { get; set; }
    public int CodePoint { get; set; } = 0x41;
    public string Text { get; set; } = "";
    public string Search { get; set; } = "";
    public UnicodeBlock? Block { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly FontCatalogService _fontCatalog = new();
    private readonly List<FontEntry> _fonts = [];
    private readonly DispatcherTimer _searchTimer;
    private Task? _initializationTask;
    private IReadOnlyList<GlyphItem> _allGlyphs = [], _glyphs = [];
    private FontEntry? _selectedFont;
    private FontVariant? _selectedVariant;
    private GlyphItem? _selectedGlyph;
    private FontTab? _selectedTab;
    private UnicodeBlock? _selectedBlock;
    private string _fontSearch = "", _codePointSearch = "", _statusText = "正在载入字体…", _composerText = "";
    private bool _isBusy = true, _showAnnotations, _restoringTab;
    private double _itemSize = 88;
    private readonly RelayCommand _copyCommand, _addCommand, _copyTextCommand, _clearTextCommand;

    public MainWindowViewModel()
    {
        Fonts = new ListCollectionView(_fonts);
        Fonts.Filter = item => item is FontEntry f && f.DisplayName.Contains(FontSearch.Trim(), StringComparison.CurrentCultureIgnoreCase);
        Fonts.GroupDescriptions.Add(new PropertyGroupDescription(nameof(FontEntry.Group)));
        Fonts.SortDescriptions.Add(new SortDescription(nameof(FontEntry.DisplayName), ListSortDirection.Ascending));
        _copyCommand = new(_ => CopyText(SelectedGlyph?.Text), _ => SelectedGlyph != null);
        _addCommand = new(p => ComposerText += (p as GlyphItem ?? SelectedGlyph)?.Text, _ => SelectedGlyph != null);
        _copyTextCommand = new(_ => CopyText(ComposerText), _ => ComposerText.Length > 0);
        _clearTextCommand = new(_ => ComposerText = "", _ => ComposerText.Length > 0);
        FindCodePointCommand = new RelayCommand(_ => ApplyGlyphFilter());
        OpenFontCommand = new RelayCommand(_ => OpenFont()); OpenFolderCommand = new RelayCommand(_ => OpenFolder());
        AddTabCommand = new RelayCommand(_ => AddTab()); CloseTabCommand = new RelayCommand(p => { if (p is FontTab tab) CloseTab(tab); });
        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _searchTimer.Tick += (_, _) => { _searchTimer.Stop(); ApplyGlyphFilter(); };
        AddTab();
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public ICollectionView Fonts { get; }
    public IReadOnlyList<FontEntry> AllFonts => _fonts;
    public ObservableCollection<FontTab> Tabs { get; } = [];
    public ObservableCollection<UnicodeBlock> Blocks { get; } = [];
    public IReadOnlyList<GlyphItem> Glyphs { get => _glyphs; private set => SetField(ref _glyphs, value); }
    public IReadOnlyList<FontVariant> Variants => SelectedFont?.Variants ?? [];
    public GlyphTypeface? CurrentFace => SelectedVariant?.GlyphTypeface ?? SelectedFont?.GlyphTypeface;
    public ICommand CopyCommand => _copyCommand;
    public ICommand AddCommand => _addCommand;
    public ICommand CopyTextCommand => _copyTextCommand;
    public ICommand ClearTextCommand => _clearTextCommand;
    public ICommand FindCodePointCommand { get; }
    public ICommand OpenFontCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand AddTabCommand { get; }
    public ICommand CloseTabCommand { get; }
    public string FontCountLabel => $"{_fonts.Count:N0} 种字体家族";
    public string GlyphCountLabel => $"{Glyphs.Count:N0} / {_allGlyphs.Count:N0} 个字符";
    public string CurrentBlockLabel => SelectedGlyph?.Block ?? "没有匹配的字符";
    public FontTab? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (_selectedTab == value || value == null) return;
            SaveTab(); SetField(ref _selectedTab, value); _restoringTab = true;
            SelectedFont = value.Font ?? _fonts.FirstOrDefault();
            if (value.Variant != null) SelectedVariant = value.Variant;
            ComposerText = value.Text; CodePointSearch = value.Search;
            SelectedBlock = Blocks.FirstOrDefault(b => b.Name == value.Block?.Name) ?? Blocks.FirstOrDefault();
            ApplyGlyphFilter(); SelectedGlyph = Glyphs.FirstOrDefault(g => g.CodePoint == value.CodePoint) ?? Glyphs.FirstOrDefault();
            _restoringTab = false;
        }
    }
    public FontEntry? SelectedFont
    {
        get => _selectedFont;
        set
        {
            if (!SetField(ref _selectedFont, value) || value == null) return;
            if (!_restoringTab && SelectedTab != null) SelectedTab.Font = value;
            OnPropertyChanged(nameof(Variants));
            SelectedVariant = value.Variants.FirstOrDefault(v => v.Weight == FontWeights.Normal && v.Style == FontStyles.Normal)
                ?? value.Variants.FirstOrDefault() ?? new FontVariant(value.GlyphTypeface);
        }
    }
    public FontVariant? SelectedVariant
    {
        get => _selectedVariant;
        set { if (SetField(ref _selectedVariant, value)) { OnPropertyChanged(nameof(CurrentFace)); LoadGlyphs(); } }
    }
    public GlyphItem? SelectedGlyph
    {
        get => _selectedGlyph;
        set { if (SetField(ref _selectedGlyph, value)) { OnPropertyChanged(nameof(CurrentBlockLabel)); _copyCommand.NotifyCanExecuteChanged(); _addCommand.NotifyCanExecuteChanged(); } }
    }
    public UnicodeBlock? SelectedBlock
    {
        get => _selectedBlock;
        set { if (SetField(ref _selectedBlock, value) && !_restoringTab) ApplyGlyphFilter(); }
    }
    public string FontSearch { get => _fontSearch; set { if (SetField(ref _fontSearch, value)) Fonts.Refresh(); } }
    public string CodePointSearch
    {
        get => _codePointSearch;
        set { if (SetField(ref _codePointSearch, value)) { _searchTimer.Stop(); if (!_restoringTab) _searchTimer.Start(); } }
    }
    public string ComposerText
    {
        get => _composerText;
        set { if (SetField(ref _composerText, value)) { _copyTextCommand.NotifyCanExecuteChanged(); _clearTextCommand.NotifyCanExecuteChanged(); } }
    }
    public string StatusText { get => _statusText; set => SetField(ref _statusText, value); }
    public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }
    public bool ShowAnnotations { get => _showAnnotations; set => SetField(ref _showAnnotations, value); }
    public double ItemSize { get => _itemSize; set => SetField(ref _itemSize, double.IsFinite(value) ? Math.Clamp(value, 56, 160) : 88); }
    public Task InitializeAsync() => _initializationTask ??= LoadFontsAsync();
    private async Task LoadFontsAsync()
    {
        try
        {
            var fonts = await Task.Run(_fontCatalog.LoadSystemFonts);
            _fonts.AddRange(fonts);
            Fonts.Refresh();
            OnPropertyChanged(nameof(FontCountLabel));
            var settings = SettingsService.Load(); ItemSize = settings.ItemSize; ShowAnnotations = settings.ShowAnnotations;
            SelectedFont = _fonts.FirstOrDefault(f => f.DisplayName == settings.FontName)
                ?? _fonts.FirstOrDefault(f => f.DisplayName == "Segoe UI") ?? _fonts.FirstOrDefault();
            StatusText = $"已载入 {_fonts.Count:N0} 个字体系列";
        }
        catch (Exception ex) { StatusText = $"字体载入失败：{ex.Message}"; }
        finally { IsBusy = false; }
    }
    public void SaveSettings() => SettingsService.Save(new(SelectedFont?.DisplayName, ItemSize, ShowAnnotations, ThemeService.Mode));
    private void LoadGlyphs()
    {
        int previous = SelectedGlyph?.CodePoint ?? 0x41;
        _allGlyphs = CurrentFace?.CharacterToGlyphMap.Keys.Where(c => c >= 0x20 && Rune.IsValid(c) && (c & 0xFFFF) < 0xFFFE)
            .Order().Select(c => new GlyphItem(c)).ToArray() ?? [];
        string? oldBlock = SelectedBlock?.Name;
        Blocks.Clear(); Blocks.Add(new UnicodeBlock(0, 0x10FFFF, "所有 Unicode 区段"));
        foreach (var block in UnicodeDataService.Blocks.Where(b => _allGlyphs.Any(g => g.CodePoint >= b.Start && g.CodePoint <= b.End))) Blocks.Add(block);
        _selectedBlock = Blocks.FirstOrDefault(b => b.Name == oldBlock) ?? Blocks[0]; OnPropertyChanged(nameof(SelectedBlock));
        ApplyGlyphFilter(); SelectedGlyph = Glyphs.FirstOrDefault(g => g.CodePoint == previous) ?? Glyphs.FirstOrDefault();
    }
    public void ApplyGlyphFilter()
    {
        _searchTimer.Stop();
        string query = CodePointSearch.Trim(); int? point = ParseCodePoint(query); var block = SelectedBlock;
        Glyphs = _allGlyphs.Where(g => (block == null || g.CodePoint >= block.Start && g.CodePoint <= block.End)
            && (query.Length == 0 || (point.HasValue ? g.CodePoint == point : g.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))).ToArray();
        SelectedGlyph = Glyphs.FirstOrDefault(g => g.CodePoint == SelectedGlyph?.CodePoint) ?? Glyphs.FirstOrDefault();
        OnPropertyChanged(nameof(GlyphCountLabel));
        StatusText = Glyphs.Count == 0 ? "当前字体或区段中没有匹配的字符" : $"{SelectedFont?.DisplayName} · {GlyphCountLabel}";
    }
    public static int? ParseCodePoint(string query)
    {
        bool prefix = query.StartsWith("U+", StringComparison.OrdinalIgnoreCase) || query.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        if (prefix) query = query[2..];
        else if (query.EnumerateRunes().Count() == 1) return query.EnumerateRunes().First().Value;
        return int.TryParse(query, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int point) && Rune.IsValid(point) ? point : null;
    }
    private void SaveTab()
    {
        if (SelectedTab == null) return;
        SelectedTab.Font = SelectedFont; SelectedTab.Variant = SelectedVariant; SelectedTab.Text = ComposerText;
        SelectedTab.CodePoint = SelectedGlyph?.CodePoint ?? 0x41; SelectedTab.Search = CodePointSearch; SelectedTab.Block = SelectedBlock;
    }
    public void AddTab() { var tab = new FontTab { Font = SelectedFont }; Tabs.Add(tab); SelectedTab = tab; }
    public void CloseTab(FontTab tab)
    {
        if (Tabs.Count <= 1) { StatusText = "至少保留一个标签页"; return; }
        int index = Tabs.IndexOf(tab);
        if (SelectedTab == tab) SelectedTab = Tabs[index > 0 ? index - 1 : 1];
        Tabs.Remove(tab);
    }
    private void CopyText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try { Clipboard.SetText(text); StatusText = "已复制到剪贴板"; }
        catch (Exception ex) { StatusText = $"复制失败：{ex.Message}"; }
    }
    private void OpenFont()
    {
        var dialog = new OpenFileDialog { Title = "打开字体文件", Multiselect = true, Filter = "字体文件 (*.ttf;*.otf;*.ttc;*.otc)|*.ttf;*.otf;*.ttc;*.otc" };
        if (dialog.ShowDialog() == true) ImportFiles(dialog.FileNames);
    }
    private void OpenFolder()
    {
        var dialog = new OpenFolderDialog { Title = "打开字体文件夹" };
        if (dialog.ShowDialog() != true) return;
        try { ImportFiles(Directory.EnumerateFiles(dialog.FolderName, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }).Where(IsFontFile).ToArray()); }
        catch (Exception ex) { StatusText = $"无法读取文件夹：{ex.Message}"; }
    }
    public static bool IsFontFile(string path) => new[] { ".ttf", ".otf", ".ttc", ".otc" }.Contains(Path.GetExtension(path).ToLowerInvariant());
    public void ImportFiles(IEnumerable<string> paths)
    {
        int loaded = 0, failed = 0; FontEntry? first = null;
        foreach (string path in paths)
        {
            try
            {
                foreach (var font in _fontCatalog.LoadFontCollection(path))
                {
                    var existing = _fonts.FirstOrDefault(f => f.SourcePath == font.SourcePath && f.DisplayName == font.DisplayName);
                    if (existing == null) { _fonts.Add(font); loaded++; }
                    first ??= existing ?? font;
                }
            }
            catch { failed++; }
        }
        Fonts.Refresh();
        FontSearch = ""; if (first != null) { CodePointSearch = ""; SelectedFont = first; }
        OnPropertyChanged(nameof(FontCountLabel)); StatusText = $"已打开 {loaded} 个字体系列" + (failed > 0 ? $"，{failed} 个文件无法读取" : "");
    }
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnPropertyChanged(name); return true;
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
