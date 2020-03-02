using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Services;
using CharacterMap.ViewModels;
using CharacterMap.Views;
using GalaSoft.MvvmLight.Ioc;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace CharacterMap.Styles
{
    public sealed partial class ItemTemplates : ResourceDictionary
    {
        public ItemTemplates()
        {
            this.InitializeComponent();
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.DataContext is ExportResult result)
            {
                _ = Launcher.LaunchFolderPathAsync(
                    Path.GetDirectoryName(result.File.Path), 
                    new FolderLauncherOptions
                    {
                        ItemsToSelect = { result.File }
                    });
            }
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.DataContext is ExportResult result)
            {
                _ = Launcher.LaunchFileAsync(result.File);
            }
        }

        private void BtnViewCollection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.DataContext is AddToCollectionResult result)
            {
                MainViewModel main = ResourceHelper.Get<ViewModelLocator>("Locator").Main;

                if (MainPage.MainDispatcher.HasThreadAccess)
                {
                    main.Settings.LastSelectedFontName = result.Font.Name;
                    main.SelectedCollection = result.Collection;
                }
                else
                {
                    _ = MainPage.MainDispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                    {
                        await WindowService.TrySwitchToWindowAsync(WindowService.MainWindow, true);
                        main.Settings.LastSelectedFontName = result.Font.Name;
                        main.SelectedCollection = result.Collection;
                    });
                }

                b.GetFirstAncestorOfType<InAppNotification>()?.Dismiss();
            }
        }

        private void FontContextFlyout_Opening(object sender, object e)
        {
            if (sender is MenuFlyout menu && menu.Target.DataContext is InstalledFont font)
            {
                FlyoutHelper.CreateMenu(
                    menu,
                    font,
                    false);
            }
        }
    }
}
