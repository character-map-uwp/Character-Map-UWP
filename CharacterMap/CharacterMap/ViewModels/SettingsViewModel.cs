using System.Collections.ObjectModel;
using Windows.ApplicationModel.Core;
using Windows.Globalization;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace CharacterMap.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private Random _random { get; } = new Random();

        protected override bool TrackAnimation => true;

        public List<GlyphAnnotation> Annotations { get; } = new ()
        {
            GlyphAnnotation.None,
            GlyphAnnotation.UnicodeHex,
            GlyphAnnotation.UnicodeIndex
        };

        [ObservableProperty] string _rampInput;
        [ObservableProperty] FontFamily _previewFontSource;
        [ObservableProperty] List<InstalledFont> _previewFonts;
        [ObservableProperty] bool _isCollectionExportEnabled = true;
        [ObservableProperty] bool _isSystemExportEnabled = true;
        [ObservableProperty] ObservableCollection<String> _rampOptions = null;

        [ObservableProperty] int _systemFamilyCount = 0;
        [ObservableProperty] int _systemFaceCount = 0;
        [ObservableProperty] string _systemExportProgress;
        [ObservableProperty] string _importedExportProgress;



        public bool ThemeHasChanged => Settings.ApplicationDesignTheme != _originalDesign;

        private List<ChangelogItem> _changelog;
        public List<ChangelogItem> Changelog => _changelog ??= CreateChangelog();

        private List<SupportedLanguage> _supportedLanguages;
        public List<SupportedLanguage> SupportedLanguages => _supportedLanguages ??= GetSupportedLanguages();

        private int _originalDesign { get; }

        private AppSettings Settings { get; } = ResourceHelper.AppSettings;

        public SettingsViewModel()
        {
            _originalDesign = Settings.ApplicationDesignTheme;
        }

        public void UpdatePreviews(InstalledFont font, FontVariant variant)
        {
            bool isSymbol = Ioc.Default.GetService<UserCollectionsService>().IsSymbolFont(font);

            // 1. Update "A B Y" Character grid previews
            // Note: it is legal for both "variant" and "font" to be NULL
            //       when calling, so test both cases.
            PreviewFontSource = variant != null && !isSymbol 
                ? new FontFamily(variant.XamlFontSource) 
                : FontFamily.XamlAutoFontFamily;

            // 2. Update FontList Previews
            var items = Enumerable.Range(1, 5).Select(i => FontFinder.Fonts[_random.Next(0, FontFinder.Fonts.Count - 1)])
                                              .OrderBy(f => f.Name)
                                              .ToList();

            if (font != null && !isSymbol && !items.Contains(font))
            {
                items.RemoveAt(0);
                items.Add(font);
            }

            PreviewFonts = items.OrderBy(f => f.Name).ToList();

            // 3. Update Ramp Options
            if (RampOptions is not null)
                RampOptions.CollectionChanged -= RampOptions_CollectionChanged;
            RampOptions = new(Settings.CustomRampOptions);
            RampOptions.CollectionChanged += RampOptions_CollectionChanged;
            RampInput = null;

            // 4. Update other things
            SystemFamilyCount = FontFinder.SystemFamilyCount;
            SystemFaceCount = FontFinder.SystemFaceCount;
        }

        private void RampOptions_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Settings.CustomRampOptions = RampOptions;
            Messenger.Send(new RampOptionsUpdatedMessage());
        }

        public void ResetFontPreview()
        {
            // Causes Bindings to re-evaluate so the UI can
            // regenerate using the correct fonts
            var list = PreviewFonts;
            PreviewFonts = null;
            PreviewFonts = list?.ToList();
        }

        private List<ChangelogItem> CreateChangelog()
        {
            // Could read this from a text file, but that's a waste of performance.
            // Naught wrong with this :P

            // Not really including bug fixes in here, just key features. The main idea
            // is to try and expose features people may not be aware exist inside the
            // application, rather than things like bug-fixes or visual changes.
            return new()
            {
                 new("Latest Update (November 2023)", // August 2023
                    "- Added support for installing opened .WOFF & .WOFF2 fonts\n" +
                    "- Added support for showing Design Script Languages in Font Properties flyout\n" +
                    "- Added 300+ new font list filters for Design Tag & Unicode Range under Supported Scripts\n" +
                    "- Added support for hiding deprecated MDL2 & Fluent glyphs by default under Settings->Advanced"),
                new("2023.8.0.0 (October 2023)", // October 2023
                    "- Added option to export installed fonts in Settings->Font Management"),
                new("2023.7.0.0 (August 2023)", // August 2023
                    "- Added support for Right-to-Left text in Type Ramp view, Quick Compare and Compare Fonts view\n" +
                    "- Added Unicode developer tools showing Unicode codepoint and UTF-16 value\n" +
                    "- Added experimental COLRv1 support to Preview Pane, Type Ramp View, Quick Compare and Compare Fonts View for Windows 11 builds > 25905\n" +
                    "    • If a glyph has both COLRv1 and COLRv0 versions, only the COLRv1 version can be seen in these views\n" +
                    "    • SVG Export or copying as SVG will only use COLRv0 glyphs\n" +
                    "    • PNG Export or copying as PNG will favour the COLRv1 glyphs"),
                new("2023.6.0.0 (June 2023)", // June 2023
                    "- Added ability to group characters by Unicode Range on character map (Ctrl + G)\n" +
                    "- Added ability to Copy as SVG (Ctrl + Shift + C)\n" +
                    "- Added ability to Copy as PNG (Ctrl + Alt + C)\n" +
                    "- Character filtering is now by Unicode Range"),
                new("2023.3.0.0 (Apr 2023)", // April 2023
                    "- Added ability to open and import WOFF2 fonts (WOFF2 fonts are converted to OTF during import)\n" +
                    "- Added font PANOSE information to Font Properties flyout"),
                new("2023.2.4.0 (Mar 2023)", // March 2023
                    "- Add ability to add default preview strings for Type Ramp view and Compare window through the suggestions popup or inside Settings view"),
                new("2023.1.2.0 (Jan 2023)", // Jan 2023
                    "- Added tabbed interface support to the Windows 11 theme\n" +
                    "- Added ability to compare all faces in a font family from Font List context menu or the \"...\" menu"),
                new("2022.3.0.0 (Dec 2022)", // Dec 2022
                    "- Added Calligraphy view to practice drawing characters in the style of the chosen font (Ctrl + I)"),
                new("2022.2.0.0 (May 2022)", // May 2022
                    "- Added support for opening folders of fonts using the Open button (Ctrl + Shift + O)\n" +
                    "- Added keyboard shortcut for opening individual font files from main window (Ctrl + O)\n" +
                    "- Added support for selecting a .ZIP archive when opening a font file and showing all the fonts in the .ZIP\n" +
                    "- Added C# WinUI 3 & C++/WinRT WinUI 3 developer features"),
                new("2022.1.2.0 (March 2022)",
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

        public static List<SupportedLanguage> GetSupportedLanguages()
        {
            List<SupportedLanguage> list  = new(
                ApplicationLanguages.ManifestLanguages
                                    .Select(language => new SupportedLanguage(language)));
            
            list.Insert(0, SupportedLanguage.SystemLanguage);
            return list;
        }

        internal async void ExportSystemAsZip()
        {
            IsSystemExportEnabled = false;
            try { 
                await ExportManager.ExportFontsAsZipAsync(
                    FontFinder.GetSystemVariants(), 
                    Localization.Get("OptionSystemFonts/Text"),
                    p => OnSyncContext(() => SystemExportProgress = p)); 
            }
            finally { IsSystemExportEnabled = true; }
        }

        internal async void ExportSystemToFolder()
        {
            IsSystemExportEnabled = false;
            try { 
                await ExportManager.ExportFontsToFolderAsync(
                    FontFinder.GetSystemVariants(),
                    p => OnSyncContext(() => SystemExportProgress = p)); 
            }
            finally { IsSystemExportEnabled = true; }
        }

        internal async void ExportAsZip()
        {
            IsCollectionExportEnabled = false;
            try { 
                await ExportManager.ExportFontsAsZipAsync(
                    FontFinder.GetImportedVariants(), 
                    Localization.Get("OptionImportedFonts/Text"),
                    p => OnSyncContext(() => ImportedExportProgress = p));
            }
            finally { IsCollectionExportEnabled = true; }
        }

        internal async void ExportToFolder()
        {
            IsCollectionExportEnabled = false;
            try { 
                await ExportManager.ExportFontsToFolderAsync(
                    FontFinder.GetImportedVariants(),
                    p => OnSyncContext(() => ImportedExportProgress = p)); 
            }
            finally { IsCollectionExportEnabled = true; }
        }

        internal void SetDesign(int selectedIndex)
        {
            Settings.ApplicationDesignTheme = selectedIndex;
            OnPropertyChanged(nameof(ThemeHasChanged));
        }

        public void LaunchReview()
        {
            _ = SystemInformation.LaunchStoreForReviewAsync();
        }

        public void Restart()
        {
            _ = CoreApplication.RequestRestartAsync(string.Empty);
        }

        public void SetLightTheme()
        {
            Settings.UserRequestedTheme = ElementTheme.Light;
        }

        public void SetDarkTheme()
        {
            Settings.UserRequestedTheme = ElementTheme.Dark;
        }

        public void SetWindowsTheme()
        {
            Settings.UserRequestedTheme = ElementTheme.Default;
        }

        public void AddRamp()
        {
            if (!string.IsNullOrWhiteSpace(RampInput))
                RampOptions.Add(RampInput);

            RampInput = null;
        }

        public void RemoveRamp(string str)
        {
            RampOptions.Remove(str);
        }
    }
}
