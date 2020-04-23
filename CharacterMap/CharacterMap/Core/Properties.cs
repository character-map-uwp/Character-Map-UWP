using CharacterMap.Controls;
using CharacterMap.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Core.Direct;
using Windows.UI.Xaml.Data;

namespace CharacterMap.Core
{
    [Bindable]
    public class Properties : DependencyObject
    {
        #region FILTER 

        /* Used to apply a filter to MenuFlyoutItem's on the filter list */

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

        #endregion

        #region TYPOGRAPHY

        /* Helper to apply TypographyFeatureInfo to a TextBlock */

        public static TypographyFeatureInfo GetTypography(DependencyObject obj)
        {
            return (TypographyFeatureInfo)obj.GetValue(TypographyProperty);
        }

        public static void SetTypography(DependencyObject obj, TypographyFeatureInfo value)
        {
            obj.SetValue(TypographyProperty, value);
        }

        public static readonly DependencyProperty TypographyProperty =
            DependencyProperty.RegisterAttached("Typography", typeof(TypographyFeatureInfo), typeof(Properties), new PropertyMetadata(null, (d,e) =>
            {
                if (d is TextBlock t)
                {
                    TypographyFeatureInfo i = e.NewValue as TypographyFeatureInfo;
                    var x = XamlDirect.GetDefault();
                    IXamlDirectObject p = x.GetXamlDirectObject(t);
                    CharacterGridView.UpdateTypography(x, p, i);
                }
            }));

        #endregion

    }
}
