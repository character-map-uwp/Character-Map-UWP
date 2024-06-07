using CharacterMap.Views;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Styles;

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
            b.GetFirstAncestorOfType<InAppNotification>()?.Dismiss();

            AddToCollectionResult result = (AddToCollectionResult)t.DataContext;

            // 1. Send a message to see if anyone wants to handle this before
            //    falling back to the default path
            CollectionRequestedMessage msg = new(result.Collection);
            WeakReferenceMessenger.Default.Send(msg);
            if (msg.Handled)
                return;

            // 2. This code path with be followed from a secondary Font-only
            //    character map view
            MainViewModel main = Ioc.Default.GetService<MainViewModel>();

            if (MainPage.MainDispatcher.HasThreadAccess)
            {
                main.Settings.LastSelectedFontName = result.Fonts.FirstOrDefault()?.Name;
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
                    await collections.AddToCollectionAsync(args.Fonts, args.Collection);
                    WeakReferenceMessenger.Default.Send(new CollectionsUpdatedMessage { SourceCollection = args.Collection });
                }
                else
                {
                    await collections.RemoveFromCollectionAsync(args.Fonts, args.Collection);
                    WeakReferenceMessenger.Default.Send(new CollectionsUpdatedMessage { SourceCollection = args.Collection });
                }
            }
            else if (b.Tag is AddToCollectionResult result)
            {
                if (result.Success && result.Collection is UserFontCollection user)
                {
                    await collections.RemoveFromCollectionAsync(result.Font, user);
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
