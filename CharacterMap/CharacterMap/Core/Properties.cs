using CharacterMap.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace CharacterMap.Core
{
    [Bindable]
    public class Properties : DependencyObject
    {
        public static BasicFontFilter GetFilter(DependencyObject obj)
        {
            return (BasicFontFilter)obj.GetValue(FilterProperty);
        }

        public static void SetFilter(DependencyObject obj, BasicFontFilter value)
        {
            obj.SetValue(FilterProperty, value);
        }

        public static readonly DependencyProperty FilterProperty =
            DependencyProperty.RegisterAttached("Filter", typeof(BasicFontFilter), typeof(Properties), new PropertyMetadata(null, (d,e) =>
            {
                if (d is MenuFlyoutItem item && e.NewValue is BasicFontFilter f)
                {
                    item.CommandParameter = f;
                    item.Text = f.DisplayTitle;
                }
            }));

    }
}
