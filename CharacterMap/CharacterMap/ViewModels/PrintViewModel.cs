using CharacterMap.Controls;
using CharacterMap.Core;
using GalaSoft.MvvmLight;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace CharacterMap.ViewModels
{
    public enum PrintLayout
    {
        Grid,
        List,
        TwoColumn
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

        private bool _showColorGlyphs = true;
        public bool ShowColorGlyphs
        {
            get => _showColorGlyphs;
            set => Set(ref _showColorGlyphs, value);
        }

        private double _glyphSize = 64d;
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

        public bool IsPortrait => Orientation == Orientation.Vertical;

        internal CharacterGridViewTemplateSettings GetTemplateSettings()
        {
            return new CharacterGridViewTemplateSettings
            {
                Size = GlyphSize,
                ShowColorGlyphs = ShowColorGlyphs,
                ShowUnicode = true,
                Typography = Typography,
                FontFamily = FontFamily,
                FontFace = Font.FontFace
            };
        }

        public static PrintViewModel Create(FontMapViewModel viewModel)
        {
            return new PrintViewModel
            {
                Typography = viewModel.SelectedTypography,
                FontFamily = viewModel.FontFamily,
                ShowColorGlyphs = viewModel.ShowColorGlyphs,
                GlyphSize = viewModel.Settings.GridSize,
                Font = viewModel.SelectedVariant
            };
        }
    }
}
