using CharacterMap.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace CharacterMap.Models
{
    public class ExportParameters : DependencyObject
    {
        public TypographyFeatureInfo Typography
        {
            get { return (TypographyFeatureInfo)GetValue(TypographyProperty); }
            set { SetValue(TypographyProperty, value); }
        }

        public static readonly DependencyProperty TypographyProperty =
            DependencyProperty.Register("Typography", typeof(TypographyFeatureInfo), typeof(ExportParameters), new PropertyMetadata(null));

        public ExportStyle Style
        {
            get { return (ExportStyle)GetValue(StyleProperty); }
            set { SetValue(StyleProperty, value); }
        }

        public static readonly DependencyProperty StyleProperty =
            DependencyProperty.Register("Style", typeof(ExportStyle), typeof(ExportParameters), new PropertyMetadata(ExportStyle.Black));
    }
}
