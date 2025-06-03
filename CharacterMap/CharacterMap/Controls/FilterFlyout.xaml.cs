﻿using System.Windows.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;

[DependencyProperty<ICommand>("FilterCommand")]
[DependencyProperty<ICommand>("CollectionSelectedCommand")]
[AttachedProperty<FrameworkElement>("UnicodeRangeSortHeader")]
public sealed partial class FilterFlyout : MenuFlyout
{
    private int _defaultCount = 0;

    private MenuFlyoutItemBase _variableOption = null;
    private MenuFlyoutItemBase _remoteOption = null;
    private MenuFlyoutItemBase _appxOption = null;
    private MenuFlyoutSeparator _fontSep = null;

    private MenuFlyoutSubItem _ops = null;

    public static IReadOnlyList<BasicFontFilter> AllFilters { get; private set; }

    public FilterFlyout()
    {
        this.InitializeComponent();
        Create();
    }

    private void Create()
    {
        // x:Bind doesn't work here because there's no Loading method on MenuFlyout
        // for the code gen to work, so the flyout needs to be created in code behind.

        Style style = ResourceHelper.Get<Style>("ThemeMenuFlyoutItemStyle");
        Style subStyle = ResourceHelper.Get<Style>("ThemeMenuFlyoutSubItemStyle");

        // 1. Top level filters
        Add(BasicFontFilter.All);
        //Add(BasicFontFilter.fsTypeDebug);
        Add(BasicFontFilter.SerifFonts);
        Add(BasicFontFilter.SansSerifFonts);
        Add(BasicFontFilter.SymbolFonts);

        // 2. Support Scripts
        var sub = AddSub("OptionSupportedScripts/Text");

        var design = AddSub("OptionDesignTag/Text", sub);
        foreach (var script in UnicodeScriptTags.Scripts)
            design.Add(BasicFontFilter.ForDesignScriptTag(script.Key, script.Value), style);

        // 2.1. Add Unicode ranges
        var unicode = AddSub("OptionUnicodeRange/Text", sub);
        var ush = GetUnicodeRangeSortHeader(this);
        if (ush.GetFirstDescendantOfType<Button>() is Button b)
        {
            // 2.1.1. Implement sorting logic
            b.Click += (s, e) =>
            {
                var tb = ush.GetFirstDescendantOfType<TextBlock>();
                var items = unicode.Items.ToList();
                unicode.Items.Clear();

                if (items[0].Tag is List<MenuFlyoutItemBase> exist)
                {
                    items[0].Tag = null;
                    foreach (var item in exist)
                        unicode.Items.Add(item);

                    tb.Text = Localization.Get("SortedByRangeLabel/Text");
                }
                else
                {
                    items[0].Tag = items;
                    foreach (var item in items.OrderBy(g => Properties.GetFilter(g)?.DisplayTitle ?? "-1"))
                        unicode.Items.Add(item);

                    tb.Text = Localization.Get("SortedByNameLabel/Text");
                }
            };

            unicode.Items.Add(new MenuFlyoutContentHost() { Content = ush });
            unicode.Items.Add(new MenuFlyoutSeparator());
        }

       // 2.1.2. Add *all* supported unicode ranges to the submenu
        foreach (var filter in UnicodeRanges.AllFilters)
            unicode.Add(filter, style);

        // 2.1.3. Add quick-access ranges to the top level menu
        AddSep(sub);
        sub.Add(BasicFontFilter.ScriptArabic, style)
            .Add(BasicFontFilter.ScriptBengali, style)
            .Add(BasicFontFilter.ScriptCyrillic, style)
            .Add(BasicFontFilter.ScriptDevanagari, style)
            .Add(BasicFontFilter.ScriptGreekAndCoptic, style)
            .Add(BasicFontFilter.ScriptCJKUnifiedIdeographs, style)
            .Add(BasicFontFilter.ScriptHiraganaAndKatakana, style)
            .Add(BasicFontFilter.ScriptHebrew, style)
            .Add(BasicFontFilter.ScriptKoreanHangul, style)
            .Add(BasicFontFilter.ScriptBasicLatin, style)
            .Add(BasicFontFilter.ScriptThai, style);


        // 3. "More" option
        _ops = AddSub("OptionMoreFilters/Text")
            .Add(BasicFontFilter.VariableFonts, style)
            .Add(BasicFontFilter.ColorFonts, style)
            .Add(BasicFontFilter.PanoseDecorativeFonts, style)
            .Add(BasicFontFilter.PanoseScriptFonts, style)
            .Add(BasicFontFilter.MonospacedFonts, style);

        _variableOption = _ops.Items[0];

        AddChild(_ops, "OptionEmoji/Text")
            .Add(BasicFontFilter.EmojiAll, style)
            .Add(BasicFontFilter.EmojiEmoticons, style)
            .Add(BasicFontFilter.EmojiDingbats, style)
            .Add(BasicFontFilter.EmojiSymbols, style);

        _fontSep = AddSep(_ops);
        _remoteOption = Create(BasicFontFilter.RemoteFonts);
        _appxOption = Create(BasicFontFilter.AppXFonts);

        _ops.Items.Add(_remoteOption);
        _ops.Items.Add(_appxOption);

        // 4. Imported fonts
        this.AddSeparator();
        Add(BasicFontFilter.ImportedFonts);

        _defaultCount = Items.Count;

        if (AllFilters is null)
             AllFilters = AllMenuItems().Where(c => Properties.GetFilter(c) is not null)
                .Select(c => Properties.GetFilter(c))
                .ToList().AsReadOnly();

        #region Helpers

        MenuFlyout Add(BasicFontFilter filter)
        {
            this.Items.Add(Create(filter));
            return this;
        }

        static MenuFlyoutSeparator AddSep(MenuFlyoutSubItem menu)
        {
            MenuFlyoutSeparator s = new();
            menu.Items.Add(s);
            return s;
        }

        MenuFlyoutSubItem AddChild(MenuFlyoutSubItem menu, string key)
        {
            MenuFlyoutSubItem item = new() { Text = Localization.Get(key), Style = subStyle };
            menu.Items.Add(item);
            return item;
        }

        MenuFlyoutSubItem AddSub(string key, MenuFlyoutSubItem parent = null)
        {
            MenuFlyoutSubItem item = new() { Text = Localization.Get(key) ?? key, Style = subStyle };
            if (parent is not null)
                parent.Items.Add(item);
            else
                this.Items.Add(item);
            return item;
        }

        MenuFlyoutItem Create(BasicFontFilter filter)
        {
            MenuFlyoutItem item = new() { Style = style };
            Core.Properties.SetFilter(item.SetAnimation(), filter);
            return item;
        }

        #endregion
    }

    public IEnumerable<MenuFlyoutItemBase> AllMenuItems()
    {
        var start = this;

        Queue<MenuFlyoutItemBase> queue = [];
        foreach (var item in this.Items)
            queue.Enqueue(item);

        while (queue.Count > 0)
        {
            var item = queue.Dequeue();
            yield return item;

            if (item is MenuFlyoutSubItem sub)
                foreach (var i in sub.Items)
                    queue.Enqueue(i);
        }
    }

    private void MenuFlyout_Opening(object sender, object e)
    {
        this.AreOpenCloseAnimationsEnabled = ResourceHelper.AllowAnimation;
        var collections = Ioc.Default.GetService<UserCollectionsService>().All;
        Style style = ResourceHelper.Get<Style>("ThemeMenuFlyoutItemStyle");

        // Reset to default menu
        while (Items.Count > _defaultCount)
            Items.RemoveAt(_defaultCount);

        // force menu width to match the source button
        foreach (var sep in Items.OfType<MenuFlyoutSeparator>())
            sep.MinWidth = this.Target.ActualWidth;

        // add users collections 
        if (collections.Count > 0)
        {
            Items.Add(new MenuFlyoutSeparator());
            foreach (var item in collections)
            {
                MenuFlyoutItem m = new() { DataContext = item, Text = item.Name, Style = style };
                Properties.SetIconString(m, item.Icon);
                m.Click += (s, a) =>
                {
                    if (m.DataContext is IFontCollection u)
                        CollectionSelectedCommand?.Execute(u);
                };
                Items.Add(m);
            }
        }

        _variableOption.SetVisible(FontFinder.HasVariableFonts);

        if (!FontFinder.HasAppxFonts && !FontFinder.HasRemoteFonts)
        {
            _fontSep.Visibility = _remoteOption.Visibility = _appxOption.Visibility = Visibility.Collapsed;
        }
        else
        {
            _fontSep.Visibility = Visibility.Visible;
            _remoteOption.SetVisible(FontFinder.HasRemoteFonts);
            _appxOption.SetVisible(FontFinder.HasAppxFonts);
        }

        var size = ResourceHelper.Get<double>("FontListFlyoutFontSize");
        var height = ResourceHelper.Get<double>("FontListFlyoutHeight");
        foreach (var item in Items)
            SetCommand(item, FilterCommand, size, height);


        // HELPER METHODS

        static void SetCommand(
                MenuFlyoutItemBase b, ICommand c, double fontSize, double height)
        {
            b.FontSize = fontSize;
            if (b is not MenuFlyoutSeparator && height > 0)
                b.Height = 40;

            if (b is MenuFlyoutSubItem i)
            {
                foreach (var child in i.Items)
                    SetCommand(child, c, fontSize, height);
            }
            else if (b is MenuFlyoutItem m)
            {
                m.Command = c;
            }
        }
    }
}
