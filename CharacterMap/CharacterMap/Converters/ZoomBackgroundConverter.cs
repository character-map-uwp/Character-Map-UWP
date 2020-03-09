using System;
using Windows.UI;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using CharacterMap.Core;

namespace CharacterMap.Converters
{
    public class ZoomBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {

            if (value != null)
            {
                var count = int.Parse(value.ToString());
                if (count > 0)
                    return new SolidColorBrush(Utils.GetAccentColor());
            }
            return new SolidColorBrush(Windows.UI.Colors.Gray);

        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
