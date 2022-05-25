using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using CharacterMap.ViewModels;
using CharacterMap.Views;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Core;
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
            else if (sender is Button bb && bb.DataContext is ExportGlyphsResult gresult)
            {
                _ = Launcher.LaunchFolderAsync(gresult.Folder);
            }
            else if (sender is Button bbb && bbb.DataContext is ExportFontFileResult fresult)
            {
                if (fresult.File != null)
                {
                    _ = Launcher.LaunchFolderPathAsync(
                        Path.GetDirectoryName(fresult.File.Path),
                        new FolderLauncherOptions
                        {
                            ItemsToSelect = { fresult.File }
                        });
                }
                else if (fresult.Folder != null)
                {
                    _ = Launcher.LaunchFolderAsync(fresult.Folder);
                }
                
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
            if (sender is Windows.UI.Xaml.Documents.Hyperlink b)
            {
                var t = b.GetFirstAncestorOfType<TextBlock>();
                AddToCollectionResult result = (AddToCollectionResult)t.DataContext;

                MainViewModel main = Ioc.Default.GetService<MainViewModel>();

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
                    null,
                    null,
                    new());
            }
        }

        private async void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b)
            {
                b.IsEnabled = false;
                b.GetFirstAncestorOfType<InAppNotification>().Dismiss();
                var collections = Ioc.Default.GetService<UserCollectionsService>();

                if (b.Tag is CollectionUpdatedArgs args)
                {
                    if (!args.IsAdd)
                    {
                        await collections.AddToCollectionAsync(args.Font, args.Collection);
                        WeakReferenceMessenger.Default.Send(new CollectionsUpdatedMessage());
                    }
                    else
                    {
                        await collections.RemoveFromCollectionAsync(args.Font, args.Collection);
                        WeakReferenceMessenger.Default.Send(new CollectionsUpdatedMessage());
                    }
                }
                else if (b.Tag is AddToCollectionResult result)
                {
                    if (result.Success)
                    {
                        await collections.RemoveFromCollectionAsync(result.Font, result.Collection);
                        WeakReferenceMessenger.Default.Send(new CollectionsUpdatedMessage());
                    }
                }
            }
        }

        private void Grid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(sender as FrameworkElement);
            if (pointer.Properties.IsMiddleButtonPressed 
                && sender is FrameworkElement f 
                && f.DataContext is InstalledFont font)
            {
                _ = FontMapView.CreateNewViewForFontAsync(font);
            }
        }
    }
}
