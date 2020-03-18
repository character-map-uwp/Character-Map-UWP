using GalaSoft.MvvmLight;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.ViewModels
{
    public class PrintViewModel : ViewModelBase
    {
        private bool _showMargins = false;
        public bool ShowMargins
        {
            get => _showMargins;
            set => Set(ref _showMargins, value);
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

        private Orientation _orientation = Orientation.Vertical;
        public Orientation Orientation
        {
            get => _orientation;
            set => Set(ref _orientation, value);
        }

        public bool IsPortrait => Orientation == Orientation.Vertical;
    }
}
