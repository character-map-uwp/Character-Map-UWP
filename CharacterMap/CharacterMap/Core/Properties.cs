using CharacterMap.Controls;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Provider;
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
    /// <summary>
    /// XAML Attached Properties
    /// </summary>
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

        #region CLIP TO BOUNDS

        public static bool GetClipToBounds(DependencyObject obj)
        {
            return (bool)obj.GetValue(ClipToBoundsProperty);
        }

        public static void SetClipToBounds(DependencyObject obj, bool value)
        {
            obj.SetValue(ClipToBoundsProperty, value);
        }

        public static readonly DependencyProperty ClipToBoundsProperty =
            DependencyProperty.RegisterAttached("ClipToBounds", typeof(bool), typeof(Properties), new PropertyMetadata(false, (d, e) =>
            {
                if (d is FrameworkElement f)
                {
                    var v = f.GetElementVisual();
                    if (e.NewValue is true)
                        v.Clip = v.Compositor.CreateInsetClip();
                    else
                        v.Clip = null;
                    
                }
            }));

        #endregion

        #region ICON

        public static IconElement GetIcon(DependencyObject obj)
        {
            return (IconElement)obj.GetValue(IconProperty);
        }

        public static void SetIcon(DependencyObject obj, IconElement value)
        {
            obj.SetValue(IconProperty, value);
        }

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.RegisterAttached("Icon", typeof(IconElement), typeof(Properties), new PropertyMetadata(null));

        #endregion

        #region PopupRoot

        public static FrameworkElement GetPopupRoot(DependencyObject obj)
        {
            return (FrameworkElement)obj.GetValue(PopupRootProperty);
        }

        public static void SetPopupRoot(DependencyObject obj, FrameworkElement value)
        {
            obj.SetValue(PopupRootProperty, value);
        }

        public static readonly DependencyProperty PopupRootProperty =
            DependencyProperty.RegisterAttached("PopupRoot", typeof(FrameworkElement), typeof(Properties), new PropertyMetadata(0));

        #endregion

        #region DEVOPTION

        public static DevOption GetDevOption(DependencyObject obj)
        {
            return (DevOption)obj.GetValue(DevOptionProperty);
        }

        public static void SetDevOption(DependencyObject obj, DevOption value)
        {
            obj.SetValue(DevOptionProperty, value);
        }

        public static readonly DependencyProperty DevOptionProperty =
            DependencyProperty.RegisterAttached("DevOption", typeof(DevOption), typeof(Properties), new PropertyMetadata(null));


        #endregion

        #region StyleKey


        public static string GetStyleKey(DependencyObject obj)
        {
            return (string)obj.GetValue(StyleKeyProperty);
        }

        public static void SetStyleKey(DependencyObject obj, string value)
        {
            obj.SetValue(StyleKeyProperty, value);
        }

        public static readonly DependencyProperty StyleKeyProperty =
            DependencyProperty.RegisterAttached("StyleKey", typeof(string), typeof(Properties), new PropertyMetadata(null));

        #endregion
    }
}
