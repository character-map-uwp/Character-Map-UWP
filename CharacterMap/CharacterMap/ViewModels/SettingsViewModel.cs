using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using Windows.Globalization;

namespace CharacterMap.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        protected override bool TrackAnimation => true;

        public List<GlyphAnnotation> Annotations => new ()
        {
            GlyphAnnotation.None,
            GlyphAnnotation.UnicodeHex,
            GlyphAnnotation.UnicodeIndex
        };

        [ObservableProperty]
        private bool _isCollectionExportEnabled = true;

        private List<ChangelogItem> _changelog;
        public List<ChangelogItem> Changelog => _changelog ??= CreateChangelog();

        private List<SupportedLanguage> _supportedLanguages;
        public List<SupportedLanguage> SupportedLanguages => _supportedLanguages ??= GetSupportedLanguages();

        private List<ChangelogItem> CreateChangelog()
        {
            // Could read this from a text file, but that's a waste of performance.
            // Naught wrong with this :P

            // Not really including bug fixes in here, just key features. The main idea
            // is to try and expose features people may not be aware exist inside the
            // application, rather than things like bug-fixes or visual changes.
            return new List<ChangelogItem>
            {
                new("Latest Update (Jan 2023)", // Jan 2023
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

        private List<SupportedLanguage> GetSupportedLanguages()
        {
            List<SupportedLanguage> list  = new(
                ApplicationLanguages.ManifestLanguages
                                    .Select(language => new SupportedLanguage(language)));
            
            list.Insert(0, SupportedLanguage.SystemLanguage);
            return list;
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
    }
}
