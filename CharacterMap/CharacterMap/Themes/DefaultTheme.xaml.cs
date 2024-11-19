using Microsoft.UI.Xaml.Controls;
using Windows.UI.Xaml;

namespace CharacterMap.Themes;

public sealed partial class DefaultTheme : ResourceDictionary
{
    public DefaultTheme()
    {
        this.InitializeComponent();

        switch (ResourceHelper.AppSettings.ApplicationDesignTheme)
        {
            case 0: // Windows 10
                this.MergeMUXC(ControlsResourcesVersion.Version1);
                this.Merge("ms-appx:///Themes/SystemThemes.xaml");
                this.Merge("ms-appx:///Themes/DefaultThemeStyles.xaml");
                break;

            case 1: // Windows 11
                this.Merge(
                    new XamlControlsResources { ControlsResourcesVersion = ControlsResourcesVersion.Version2 }
                        .Merge("ms-appx:///Styles/TabViewFluent.xaml")
                        .Merge("ms-appx:///Themes/SystemThemes.xaml")
                        .Merge("ms-appx:///Themes/FluentThemeStyles.xaml"));
                break;

            case 2: // Classic Theme
                this.MergeMUXC(ControlsResourcesVersion.Version1)
                    .Merge("ms-appx:///Themes/SystemThemes.xaml")
                    .Merge("ms-appx:///Themes/ClassicThemeStyles.xaml");
                break;

            case 3: // Zune Theme
                this.MergeMUXC(ControlsResourcesVersion.Version1)
                    .Merge("ms-appx:///Themes/ZuneThemeStyles.xaml");
                break;

            case 4: // Material
                this.Merge(
                    new XamlControlsResources { ControlsResourcesVersion = ControlsResourcesVersion.Version2 }
                        .Merge("ms-appx:///Styles/TabViewFluent.xaml")
                        .Merge("ms-appx:///Themes/SystemThemes.xaml")
                        .Merge("ms-appx:///Themes/MaterialThemeStyles.xaml"));

                ThemeIconGlyph.EnableMaterialIcons();
                break;
        }
    }
}
