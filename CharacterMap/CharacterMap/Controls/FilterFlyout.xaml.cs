using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using CommunityToolkit.Mvvm.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls
{
    public sealed partial class FilterFlyout : MenuFlyout
    {
        private int _defaultCount = 0;

        public ICommand FilterCommand
        {
            get { return (ICommand)GetValue(FilterCommandProperty); }
            set { SetValue(FilterCommandProperty, value); }
        }

        public static readonly DependencyProperty FilterCommandProperty =
            DependencyProperty.Register(nameof(FilterCommand), typeof(ICommand), typeof(FilterFlyout), new PropertyMetadata(null));

        public ICommand CollectionSelectedCommand
        {
            get { return (ICommand)GetValue(CollectionSelectedCommandProperty); }
            set { SetValue(CollectionSelectedCommandProperty, value); }
        }

        public static readonly DependencyProperty CollectionSelectedCommandProperty =
            DependencyProperty.Register(nameof(CollectionSelectedCommand), typeof(ICommand), typeof(FilterFlyout), new PropertyMetadata(null));

        private MenuFlyoutItemBase _variableOption = null;
        private MenuFlyoutItemBase _remoteOption = null;
        private MenuFlyoutItemBase _appxOption = null;
        private MenuFlyoutSeparator _fontSep = null;

        public FilterFlyout()
        {
            this.InitializeComponent();
            Create();
        }

        private void Create()
        {
            // x:Bind doesn't work here because there's no Loading method on MenuFlyout
            // for the code gen to work, so the flyout needs to be created in code behind.

            Add(BasicFontFilter.All);
            Add(BasicFontFilter.SerifFonts);
            Add(BasicFontFilter.SansSerifFonts);
            Add(BasicFontFilter.SymbolFonts);

            AddSub("OptionSupportedScripts/Text")
                .Add(BasicFontFilter.ScriptArabic)
                .Add(BasicFontFilter.ScriptCyrillic)
                .Add(BasicFontFilter.ScriptGreekAndCoptic)
                .Add(BasicFontFilter.ScriptHebrew)
                .Add(BasicFontFilter.ScriptBasicLatin)
                .Add(BasicFontFilter.ScriptThai)
                .Add(BasicFontFilter.ScriptCJKUnifiedIdeographs)
                .Add(BasicFontFilter.ScriptKoreanHangul);

            var ops = AddSub("OptionMoreFilters/Text")
                .Add(BasicFontFilter.ColorFonts)
                .Add(BasicFontFilter.PanoseDecorativeFonts)
                .Add(BasicFontFilter.PanoseScriptFonts)
                .Add(BasicFontFilter.MonospacedFonts)
                .Add(BasicFontFilter.VariableFonts);

            _variableOption = ops.Items.Last();

            AddChild(ops, "OptionEmoji/Text")
                .Add(BasicFontFilter.EmojiAll)
                .Add(BasicFontFilter.EmojiEmoticons)
                .Add(BasicFontFilter.EmojiDingbats)
                .Add(BasicFontFilter.EmojiSymbols);

            _fontSep = AddSep(ops);
            _remoteOption = ops.Add(BasicFontFilter.RemoteFonts);
            _appxOption = ops.Add(BasicFontFilter.AppXFonts);

            this.AddSeparator();
            Add(BasicFontFilter.ImportedFonts);


            _defaultCount = Items.Count;


            #region Helpers

            MenuFlyout Add(BasicFontFilter filter)
            {
                this.Items.Add(Create(filter));
                return this;
            }

            static MenuFlyoutSeparator AddSep(MenuFlyoutSubItem menu)
            {
                var s = new MenuFlyoutSeparator();
                menu.Items.Add(s);
                return s;
            }

            static MenuFlyoutSubItem AddChild(MenuFlyoutSubItem menu, string key)
            {
                MenuFlyoutSubItem item = new() { Text = Localization.Get(key) };
                menu.Items.Add(item);
                return item;
            }

            MenuFlyoutSubItem AddSub(string key)
            {
                MenuFlyoutSubItem item = new() { Text = Localization.Get(key) };
                this.Items.Add(item);
                return item;
            }

            MenuFlyoutItem Create(BasicFontFilter filter)
            {
                MenuFlyoutItem item = new();
                Core.Properties.SetFilter(item, filter);
                return item;
            }

            #endregion
        }

        private void MenuFlyout_Opening(object sender, object e)
        {
            var collections = Ioc.Default.GetService<UserCollectionsService>().Items;

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
                    MenuFlyoutItem m = new() { DataContext = item, Text = item.Name };
                    m.Click += (s, a) =>
                    {
                        if (m.DataContext is UserFontCollection u)
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
}
