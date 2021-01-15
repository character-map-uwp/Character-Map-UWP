using CharacterMap.Controls;
using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Data.Text;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace CharacterMap.ViewModels
{
    public class UnicodeCategoryModel : ViewModelBase
    {
        public UnicodeGeneralCategory Category { get; }

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }

        public string DisplayName { get; }

        public UnicodeCategoryModel(UnicodeGeneralCategory category)
        {
            Category = category;
            DisplayName = Humanizer.EnumHumanizeExtensions.Humanize(category);
        }
    }

    public class PrintViewModel : ViewModelBase
    {
        public FontVariant Font { get; set; }

        public TypographyFeatureInfo Typography { get; set; }

        public FontFamily FontFamily { get; set; }

        private bool _showMargins = false;
        public bool ShowMargins
        {
            get => _showMargins;
            set => Set(ref _showMargins, value);
        }

        private bool _showBorders = false;
        public bool ShowBorders
        {
            get => _showBorders;
            set => Set(ref _showBorders, value);
        }

        private bool _hideWhitespace = true;
        public bool HideWhitespace
        {
            get => _hideWhitespace;
            set
            {
                if (value != _hideWhitespace)
                {
                    _hideWhitespace = value;
                    UpdateCharacters();
                    OnPropertyChanged();
                }
            }
        }

        private bool _showColorGlyphs = true;
        public bool ShowColorGlyphs
        {
            get => _showColorGlyphs;
            set => Set(ref _showColorGlyphs, value);
        }

        private double _glyphSize = 70d;
        public double GlyphSize
        {
            get => _glyphSize;
            set => Set(ref _glyphSize, value);
        }

        private double _horizontalMargin = 44d;
        public double HorizontalMargin
        {
            get => _horizontalMargin;
            set => Set(ref _horizontalMargin, value);
        }

        private double _verticalMargin = 44d;
        public double VerticalMargin
        {
            get => _verticalMargin;
            set => Set(ref _verticalMargin, value);
        }

        private GlyphAnnotation _annotation = GlyphAnnotation.None;
        public GlyphAnnotation Annotation
        {
            get => _annotation;
            set => Set(ref _annotation, value);
        }

        private PrintLayout _layout = PrintLayout.Grid;
        public PrintLayout Layout
        {
            get => _layout;
            set => Set(ref _layout, value);
        }

        private Orientation _orientation = Orientation.Vertical;
        public Orientation Orientation
        {
            get => _orientation;
            set => Set(ref _orientation, value);
        }
        public IReadOnlyList<Character> Characters { get; set; }

        private IList<UnicodeCategoryModel> _categories;
        public IList<UnicodeCategoryModel> Categories
        {
            get => _categories;
            private set => Set(ref _categories, value);
        }

        private int _firstPage = 1;
        public int FirstPage
        {
            get => _firstPage;
            set => Set(ref _firstPage, value);
        }

        private int _pageCount = 50;
        public int PagesToPrint
        {
            get => _pageCount;
            set => Set(ref _pageCount, value);
        }

        public bool IsPortrait => Orientation == Orientation.Vertical;

        internal CharacterGridViewTemplateSettings GetTemplateSettings()
        {
            return new CharacterGridViewTemplateSettings
            {
                Size = GlyphSize,
                ShowColorGlyphs = ShowColorGlyphs,
                Annotation = GlyphAnnotation.None,
                Typography = Typography,
                FontFamily = FontFamily,
                FontFace = Font.FontFace
            };
        }

        public void UpdateCategories(IList<UnicodeCategoryModel> value)
        {
            _categories = value;
            UpdateCharacters();
            OnPropertyChanged(nameof(Categories));
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

        private PrintViewModel()
        {
            Categories = Unicode.CreateCategoriesList();
        }

        public static PrintViewModel Create(FontMapViewModel viewModel)
        {
            var model = new PrintViewModel
            {
                ShowColorGlyphs = viewModel.ShowColorGlyphs,
                Typography = viewModel.SelectedTypography,
                FontFamily = viewModel.FontFamily,
                Font = viewModel.SelectedVariant,
                Annotation = viewModel.Settings.GlyphAnnotation
            };

            model.UpdateCharacters();
            return model;
        }
    }
}
