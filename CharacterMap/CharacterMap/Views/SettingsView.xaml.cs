using CharacterMap.Controls;
using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.ApplicationModel.Core;
using Windows.Globalization;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Views
{
    public class ChangelogItem
    {
        public ChangelogItem(string header, string content)
        {
            Header = header;
            Content = content;
        }

        public string Header { get; set; }
        public string Content { get; set; }
    }

    public sealed partial class SettingsView : ViewBase
    {
        private Random _random { get; } = new Random();

        public AppSettings Settings { get; }

        public UserCollectionsService FontCollections { get; }

        public List<SupportedLanguage> SupportedLanguages { get; }

        public List<ChangelogItem> Changelog { get; }

        public bool IsOpen { get; private set; }

        private bool _isCollectionExportEnabled = true;
        public bool IsCollectionExportEnabled
        {
            get => _isCollectionExportEnabled;
            set => Set(ref _isCollectionExportEnabled, value);
        }

        private GridLength _titleBarHeight = new GridLength(32);
        public GridLength TitleBarHeight
        {
            get => _titleBarHeight;
            set => Set(ref _titleBarHeight, value);
        }

        public int GridSize
        {
            get { return (int)GetValue(GridSizeProperty); }
            set { SetValue(GridSizeProperty, value); }
        }

        public static readonly DependencyProperty GridSizeProperty =
            DependencyProperty.Register(nameof(GridSize), typeof(int), typeof(SettingsView), new PropertyMetadata(0d));

        public List<GlyphAnnotation> Annotations { get; } = new List<GlyphAnnotation>
        {
            GlyphAnnotation.None,
            GlyphAnnotation.UnicodeHex,
            GlyphAnnotation.UnicodeIndex
        };

        private bool _themeSupportsShadows = false;
        private bool _themeSupportsDark = false;
        private NavigationHelper _navHelper { get; } = new NavigationHelper();

        public SettingsView()
        {
            Settings = ResourceHelper.AppSettings;
            FontCollections = Ioc.Default.GetService<UserCollectionsService>();
            WeakReferenceMessenger.Default.Register<AppSettingsChangedMessage>(this, (o, m) => OnAppSettingsUpdated(m));
            WeakReferenceMessenger.Default.Register<FontListCreatedMessage>(this, (o, m) => UpdateExport());

            GridSize = Settings.GridSize;

            this.InitializeComponent();

            CompositionFactory.SetupOverlayPanelAnimation(this);

            FontNamingSelection.SelectedIndex = (int)Settings.ExportNamingScheme;

            SupportedLanguages = new List<SupportedLanguage>(
                ApplicationLanguages.ManifestLanguages.
                Select(language => new SupportedLanguage(language)));
            SupportedLanguages.Insert(0, SupportedLanguage.SystemLanguage);

            Changelog = CreateChangelog();

            _themeSupportsShadows = ResourceHelper.Get<Boolean>("SupportsShadows");
            _themeSupportsDark = ResourceHelper.Get<Boolean>("SupportsDarkTheme");

            _navHelper.BackRequested += (s, e) => Hide();

            //UpdateStyle();
        }

        void OnAppSettingsUpdated(AppSettingsChangedMessage msg)
        {
            if (!Dispatcher.HasThreadAccess)
            {
                _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    OnAppSettingsUpdated(msg);
                });
                return;
            }
            switch (msg.PropertyName)
            {
                case nameof(Settings.UserRequestedTheme):
                    OnPropertyChanged(nameof(Settings));
                    break;
                case nameof(Settings.GridSize):
                    // We can't direct bind here as it may be updated from a different UI Thread.
                    GridSize = Settings.GridSize;
                    break;
                case nameof(Settings.ApplicationDesignTheme):
                    UpdateStyle();
                    break;
            }
        }


        private void UpdateExport()
        {
            this.RunOnUI(() =>
            {
                ImportedExportPanel.SetVisible(FontFinder.ImportedFonts.Count > 0);
            });
        }

        public void Show(FontVariant variant, InstalledFont font)
        {
            if (IsOpen)
                return;


            StartShowAnimation();
            this.Visibility = Visibility.Visible;

            if (!CompositionFactory.UISettings.AnimationsEnabled)
            {
                this.GetElementVisual().Opacity = 1;
                this.GetElementVisual().Properties.InsertVector3(CompositionFactory.TRANSLATION, Vector3.Zero);
            }

            // 1. Focus the close button to ensure keyboard focus is retained inside the settings panel
            Presenter.SetDefaultFocus();

#pragma warning disable CS0618 // ChangeView doesn't work well when not properly visible
            ContentScroller.ScrollToVerticalOffset(0);
#pragma warning restore CS0618

            // 2. Get the fonts used for Font List  & Character Grid previews
            // Note: it is legal for both "variant" and "font" to be NULL
            //       when calling, so test both cases.
            bool isSymbol = FontCollections.IsSymbolFont(font);

            Preview1.FontFamily = Preview2.FontFamily = Preview3.FontFamily 
                = variant != null && !isSymbol ? new FontFamily(variant.XamlFontSource) : FontFamily.XamlAutoFontFamily;

            var items = Enumerable.Range(1, 5).Select(i => FontFinder.Fonts[_random.Next(0, FontFinder.Fonts.Count - 1)])
                                              .OrderBy(f => f.Name)
                                              .ToList();

            if (font != null && !isSymbol && !items.Contains(font))
            {
                items.RemoveAt(0);
                items.Add(font);
            }

            LstFontFamily.ItemsSource =  items.OrderBy(f => f.Name).ToList();
            
            // 3. Set correct Developer features language
            UpdateExport();

            Presenter.SetTitleBar();
            IsOpen = true;

            _navHelper.Activate();

        }

        public void Hide()
        {
            _navHelper.Deactivate();

            TitleBarHelper.RestoreDefaultTitleBar();
            IsOpen = false;
            this.Visibility = Visibility.Collapsed;
            WeakReferenceMessenger.Default.Send(new ModalClosedMessage());
        }

        private void StartShowAnimation()
        {
            if (!Settings.UseSelectionAnimations)
                return;

            List<UIElement> elements = new List<UIElement> { this, MenuColumn, ContentBorder };
            //elements.AddRange(LeftPanel.Children);
            CompositionFactory.PlayEntrance(elements, 0, 200);

            UpdateStyle();

            //elements.Clear();
            //elements.AddRange(RightPanel.Children);
            //Composition.PlayEntrance(elements, 0, 200);
        }

        private void View_Loading(FrameworkElement sender, object args)
        {
           
        }

        private void View_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateStyle();

            ((RadioButton)MenuColumn.Children.First()).IsChecked = true;

            // Set the settings that can't be set with bindings
            if (_themeSupportsDark)
            {
                switch (Settings.UserRequestedTheme)
                {
                    case ElementTheme.Default:
                        ThemeSystem.IsChecked = true;
                        break;
                    case ElementTheme.Light:
                        ThemeLight.IsChecked = true;
                        break;
                    case ElementTheme.Dark:
                        ThemeDark.IsChecked = true;
                        break;
                }
            }

            if (Settings.UseFontForPreview)
                UseActualFont.IsChecked = true;
            else
                UseSystemFont.IsChecked = true;
        }


        /* Work around to avoid binding threading issues */
        private int GetGridSize(int s) => s;

        private void UpdateGridSize(double d)
        {
            GridSize = Settings.GridSize = (int)d;
        }

        private void BtnReview_Click(object sender, RoutedEventArgs e)
        {
            _ = SystemInformation.LaunchStoreForReviewAsync();
        }

        private void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            _ = CoreApplication.RequestRestartAsync(string.Empty);
        }

        private void ThemeLight_Checked(object sender, RoutedEventArgs e)
        {
            Settings.UserRequestedTheme = ElementTheme.Light;
        }

        private void ThemeDark_Checked(object sender, RoutedEventArgs e)
        {
            Settings.UserRequestedTheme = ElementTheme.Dark;
        }

        private void ThemeSystem_Checked(object sender, RoutedEventArgs e)
        {
            Settings.UserRequestedTheme = ElementTheme.Default;
        }

        private void FontNamingSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.ExportNamingScheme = (ExportNamingScheme)((RadioButtons)sender).SelectedIndex;
        }

        private void UseSystemFont_Checked(object sender, RoutedEventArgs e)
        {
            Settings.UseFontForPreview = false;
            ResetFontPreview();
        }

        private void UseActualFont_Checked(object sender, RoutedEventArgs e)
        {
            Settings.UseFontForPreview = true;
            ResetFontPreview();
        }

        private void ResetFontPreview()
        {
            var items = LstFontFamily.ItemsSource;
            LstFontFamily.ItemsSource = null;
            LstFontFamily.ItemsSource = items;
        }

        public void SelectedLanguageToString(object selected) => 
            Settings.AppLanguage = selected is SupportedLanguage s ? s.LanguageID : "en-US";


        private void MenuItem_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton item
              && item.Tag is Panel panel
              && panel.Visibility == Visibility.Collapsed)
            {
                // X: Ensure all settings panels are hidden
                foreach (var child in ContentPanel.Children.OfType<FrameworkElement>())
                {
                    // X: Deactivate old content if supported
                    if (child.Visibility == Visibility.Visible
                        && child is Panel p
                        && p.Children.Count == 1
                        && p.Children[0] is IActivateableControl d)
                        d.Deactivate();

                    child.Visibility = Visibility.Collapsed;
                }

                // X: Reset scroll position
                ContentScroller.ChangeView(null, 0, null, true);

                // X: Activate new content if supported
                if (panel.Children.Count == 1 && panel.Children[0] is IActivateableControl a)
                {
                    a.Activate();
                    VisualStateManager.GoToState(this, ContentScrollDisabledState.Name, false);
                }
                else
                    VisualStateManager.GoToState(this, ContentScrollEnabledState.Name, false);


                // X: Start child animation
                if (Settings.UseSelectionAnimations)
                    CompositionFactory.PlayEntrance(panel.Children.OfType<UIElement>().ToList(), 0, 80);

                // X: Show selected panel
                panel.Visibility = Visibility.Visible;
            }
        }

        internal async void ExportAsZip()
        {
            IsCollectionExportEnabled = false;
            try { await ExportManager.ExportFontsAsZipAsync(FontFinder.GetImportedVariants(), Localization.Get("OptionImportedFonts/Text")); }
            finally { IsCollectionExportEnabled = true; }
        }

        internal async void ExportToFolder()
        {
            IsCollectionExportEnabled = false;
            try { await ExportManager.ExportFontsToFolderAsync(FontFinder.GetImportedVariants()); }
            finally { IsCollectionExportEnabled = true; }
        }

        List<ChangelogItem> CreateChangelog()
        {
            // Could read this from a text file, but that's a waste of performance.
            // Naught wrong with this :P

            // Not really including bug fixes in here, just key features. The main idea
            // is to try and expose features people may not be aware exist inside the
            // application, rather than things like bug-fixes or visual changes.
            return new List<ChangelogItem>
            {
                new("Latest Release", // March 2002
                    "- Added support for bulk adding and removing fonts from Collections (in Settings)"),
                new("2021.7.4.0 (October 2021)", // October
                    "- Added support for navigating backwards using mouse and keyboard navigation buttons, and Alt + Left\n" +
                    "- Added support for changing application design with themes for Windows 11, Classic Windows and Zune Desktop"),
                new("2021.4.0.0 (July 2021)", // July
                    "- Added Export Characters view (Ctrl + E)\n" +
                    "- Quick Compare (Ctrl + Q) now supports comparing typography variations and variable axis on the same font face\n" +
                    "- Copy pane (Ctrl + B) now supports editing and cursor positioning\n" +
                    "- Double clicking a character will now add it to the copy pane"),
                new("2021.3.0.0 (May 2021)",
                    "- Added glyph name and search support for Segoe Fluent Icons\n" +
                    "- Added Visual Basic developer features\n" +
                    "- Added ability to compare individual Font Face's with Quick Compare view (Ctrl + Q)"),
                new("2021.2.0.0 (Feb 2021)",
                    "- Added ability to open and import WOFF fonts (WOFF fonts are converted to OTF during import)\n" +
                    "- Added C++/CX, C++/WinRT & Xamarin.Forms developer features\n" +
                    "- Copying Path Icon from developer code now copies the path with typography applied\n" +
                    "- Added Adobe Glyph List mapping support for a font's post table names\n" +
                    "- Added character filtering by Unicode category to main view\n" +
                    "- Added Fullscreen keyboard shortcut (F11)\n" +
                    "- Added Compare Fonts view (Ctrl + K)"),
                new("2021.1.0.0 (Jan 2021)",
                    "- Added Font list search\n" +
                    "- Added ability to see all typographic variations for a single character from the character preview pane\n" +
                    "- Added support for a Font's own custom glyph names in search and character preview"),
                new("2020.18.0.0 (Aug 2020)",
                    "- Added copy pane (Ctrl + B)\n" +
                    "- Added 'Toggle Preview Pane' keyboard shortcut (Ctrl + R)\n" +
                    "- Added 'Toggle Font List' keyboard shortcut (Ctrl + L)\n" +
                    "- Added 'Increase Font size' keyboard shortcut (Ctrl + +)\n" +
                    "- Added 'Decrease Font size' keyboard shortcut (Ctrl + -)\n" +
                    "- Added 'Focus Search' keyboard shortcut (Ctrl + F)\n" +
                    "- Added a context menu to the character grid allowing you to save or copy any glyph without selecting\n" +
                    "- Added compact overlay support"),
                new("2020.15.0.0 (May 2020)",
                    "- Added 'Type-Ramp' view with support for Variable Font axis (Ctrl + T)"),
                new("2020.12.0.0 (April 2020)",
                    "- Added advanced Font List filters (by supported script, emoji, characters sets, etc.)\n" +
                    "- Added ability to export fonts in custom collections to ZIP files or to folders\n" +
                    "- Added PathIcon developer code\n" +
                    "- Added ability to export any Font to a Font file (Ctrl + S)\n" +
                    "- New Fluent UI design"),
                new("2020.9.0.0 (March 2020)",
                    "- Added printing support (Ctrl + P)\n" +
                    "- Added ability to export colour glyphs (COLR) to SVG files with correct colour layers\n" +
                    "- New Settings UI"),
                new("2020.3.0.0 (February 2020)",
                    "- Added ability to export raw SVG glyphs from SVG fonts\n" +
                    "- Added ability to export raw PNG glyphs from fonts with Bitmap PNG glyphs\n" +
                    "- Added support for user created font collections\n" +
                    "- App can now detect when a user installs new fonts to the system"),
            };
        }

        void UpdateStyle()
        {
            //string key = Settings.ApplicationDesignTheme == 0 ? "Default" : "FUI";
            //if (Settings.ApplicationDesignTheme == 2) key = "Zune";
            //var controls = this.GetDescendants().OfType<FrameworkElement>().Where(e => e is not IThemeableControl && Properties.GetStyleKey(e) is not null).ToList();
            //foreach (var p in controls)
            //{
            //    string target = $"{key}{Properties.GetStyleKey(p)}";
            //    Style style = ResourceHelper.Get<Style>(this, target);
            //    p.Style = style;
            //}

            //ResourceHelper.SendThemeChanged();


            string key = Settings.ApplicationDesignTheme switch
            {
                1 => "FUI",
                2 => " Zune",
                _ => "Default"
            };
            bool t = VisualStateManager.GoToState(this, $"{key}ThemeState", true);
        }



        /* CONVERTERS */

        Visibility ShowUnicode(GlyphAnnotation annotation)
        {
            return annotation != GlyphAnnotation.None ? Visibility.Visible : Visibility.Collapsed;
        }


        private void Design_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.ApplicationDesignTheme = ((ComboBox)sender).SelectedIndex;
        }
    }
}
