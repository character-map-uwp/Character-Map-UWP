using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;

namespace CharacterMap.ViewModels
{
    public class ExportViewModel : ViewModelBase
    {
        #region Properties
        private InstalledFont _font { get; }
        public FontVariant Font { get; set; }

        public CharacterRenderingOptions Options { get; set; }

        public IReadOnlyList<Character> Characters { get => GetV<IReadOnlyList<Character>>(); private set => Set(value); }
        public IList<UnicodeCategoryModel> Categories { get => GetV<IList<UnicodeCategoryModel>>(); private set => Set(value); }

        public bool HideWhitespace          { get => GetV(false); set => Set(value); }
        public double GlyphSize             { get => GetV(0d); set => Set(value); }
        public Color GlyphColor             { get => GetV(Colors.White); set => Set(value); }
        public bool IsWhiteChecked          { get => GetV(false); set => Set(value); }
        public bool IsBlackChecked          { get => GetV(false); set => Set(value); }
        public bool IsExporting             { get => GetV(false); set => Set(value); }
        public int SelectedFormat           { get => GetV((int)ExportFormat.Png); set => Set(value); }
        public string ExportMessage         { get => Get<string>(); set => Set(value); }

        public ElementTheme PreviewTheme { get => GetV(ResourceHelper.GetEffectiveTheme()); set => Set(value); }

        public bool IsPngFormat => SelectedFormat == (int)ExportFormat.Png;

        #endregion

        DispatcherQueue _dispatcherQueue { get; }

        public ExportViewModel(FontMapViewModel viewModel)
        {
            _font       = viewModel.SelectedFont;
            Categories  = viewModel.SelectedGlyphCategories.ToList(); // Makes a copy of the list
            Font        = viewModel.RenderingOptions.Variant;
            Options     = viewModel.RenderingOptions;
            GlyphSize  = viewModel.Settings.PngSize;

            IsWhiteChecked = ResourceHelper.GetEffectiveTheme() == ElementTheme.Dark;
            IsBlackChecked = ResourceHelper.GetEffectiveTheme() == ElementTheme.Light;

            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            UpdateCharacters();
        }

        protected override void OnPropertyChangeNotified(string property)
        {
            switch (property)
            {
                case nameof(GlyphColor):
                    if (GlyphColor != Colors.Black)
                        IsBlackChecked = false;
                    if (GlyphColor != Colors.White)
                        IsWhiteChecked = false;
                    break;

                case nameof(IsWhiteChecked) when IsWhiteChecked:
                    GlyphColor = Colors.White;
                    break;

                case nameof(IsBlackChecked) when IsBlackChecked:
                    GlyphColor = Colors.Black;
                    break;

                case nameof(HideWhitespace):
                    UpdateCharacters();
                    break;

                case nameof(SelectedFormat):
                    OnPropertyChanged(nameof(IsPngFormat));
                    break;
            }
        }

        private void UpdateCharacters()
        {
            // Fast path : all characters;
            if (!Categories.Any(c => !c.IsSelected) && !HideWhitespace)
            {
                Characters = Font.Characters;
                return;
            }

            // Filter characters
            var chars = Font.Characters.AsEnumerable();
            if (HideWhitespace)
                chars = Font.Characters.Where(c => !Unicode.IsWhiteSpaceOrControl(c.UnicodeIndex));
            foreach (var cat in Categories.Where(c => !c.IsSelected))
                chars = chars.Where(c => !Unicode.IsInCategory(c.UnicodeIndex, cat.Category));

            Characters = chars.ToList();
        }

        public void UpdateCategories(IList<UnicodeCategoryModel> value)
        {
            Set(value, nameof(Categories), false);
            UpdateCharacters();
            OnPropertyChanged(nameof(Categories));
        }

        public async void StartExport()
        {
            ExportOptions export = new((ExportFormat)SelectedFormat, ExportStyle.Black) 
            { 
                PreferredColor = GlyphColor, 
                PreferredSize = GlyphSize 
            };

            IsExporting = true;

            await ExportManager.ExportFontToFolderAsync(_font, Options, Characters, export, (index, count) =>
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    ExportMessage = $"Exporting glyph {index} of {count}";
                });
            });

            ExportMessage = "";
            IsExporting = false;
        }

        public void ToggleTheme()
        {
            PreviewTheme = PreviewTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
        }
    }
}
