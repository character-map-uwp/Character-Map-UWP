using CharacterMap.Helpers;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace CharacterMap.Themes
{
    public sealed partial class DefaultTheme : ResourceDictionary
    {
        public DefaultTheme()
        {
            this.InitializeComponent();

            switch (ResourceHelper.AppSettings.ApplicationDesignTheme)
            {
                case 0:
                    this.MergedDictionaries.Add(new XamlControlsResources { ControlsResourcesVersion = ControlsResourcesVersion.Version1 });
                    this.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/SystemThemes.xaml") });
                    this.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/DefaultThemeStyles.xaml") });
                    break;

                case 1:
                    this.MergedDictionaries.Add(new XamlControlsResources { ControlsResourcesVersion = ControlsResourcesVersion.Version2 });
                    this.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/SystemThemes.xaml") });
                    this.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/FluentThemeStyles.xaml") });
                    break;

                case 2:
                    this.MergedDictionaries.Add(new XamlControlsResources { ControlsResourcesVersion = ControlsResourcesVersion.Version1 });
                    this.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/SystemThemes.xaml") });
                    this.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/ClassicThemeStyles.xaml") });
                    break;

                case 3:
                    this.MergedDictionaries.Add(new XamlControlsResources { ControlsResourcesVersion = ControlsResourcesVersion.Version1 });
                    this.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/ZuneThemeStyles.xaml") });
                    break;
            }
        }
    }
}
