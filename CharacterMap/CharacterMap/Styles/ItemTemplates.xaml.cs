using CharacterMap.Views;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Styles;

public class TextResizeHelper
{
    long token = 0;
    public TextResizeHelper(TextBlock textBlock)
    {
        TextBlock = textBlock;
        token = textBlock.RegisterPropertyChangedCallback(TextBlock.TextProperty, callback);
    }

    private async void callback(DependencyObject sender, DP dp)
    {
        var v = Properties.GetEnableAutoScaleAnimation(sender);
        if (v)
        {
            Properties.SetEnableAutoScaleAnimation(sender, false);
            await Task.Delay(16);
            Properties.SetEnableAutoScaleAnimation(sender, true);
        }
    }

    public TextBlock TextBlock { get; }
}

[AttachedProperty<bool>("TextChangeBlock")]
[AttachedProperty<TextResizeHelper>("TextResizeHelper")]
public sealed partial class ItemTemplates : ResourceDictionary
{
    private Debouncer _textBlocker = new(16);

    public ItemTemplates()
    {
        this.InitializeComponent();
    }

    static partial async void OnTextChangeBlockChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock t && GetTextResizeHelper(t) is null)
        {
            if (e.NewValue is bool b && b)
            {
                var helper = new TextResizeHelper(t);
                SetTextResizeHelper(t, helper);
            }
        }
    }

    private async void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b)
            return;

        if (b.DataContext is ExportResult result)
        {
            _ = Launcher.LaunchFolderPathAsync(
                    Path.GetDirectoryName(result.File.Path),
                    new FolderLauncherOptions
                    {
                        ItemsToSelect = { result.File }
                    });
        }
        else if (b.DataContext is ExportCharactersResult gresult)
        {
            _ = Launcher.LaunchFolderAsync(gresult.Folder);
        }
        else if (b.DataContext is ExportFontFileResult fresult)
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
        else if (b.DataContext is SubsetResultMessage { File: StorageFile file })
        {
            _ = Launcher.LaunchFolderPathAsync(
                    Path.GetDirectoryName(file.Path),
                    new FolderLauncherOptions
                    {
                        ItemsToSelect = { file }
                    });
        }
    }

    private void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b)
            return;
        
        if (b.DataContext is ExportResult result)
            _ = Launcher.LaunchFileAsync(result.File);
        else if (b.DataContext is SubsetResultMessage { Font: not null, File: not null } srm)
            _ =  FontMapView.CreateNewViewForFontAsync(srm.Font, srm.File);

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

            var svc = Ioc.Default.ViewService();
            if (svc.MainView.HasThreadAccess)
            {
                main.Settings.LastSelectedFontName = result.Fonts.FirstOrDefault()?.Name;
                main.SelectedCollection = result.Collection;
            }
            else
            {
                _ = svc.MainView.RunOnAsync(async () =>
                {
                    await svc.TrySwitchToViewAsync(svc.MainView, true);
                    main.Settings.LastSelectedFontName = result.Font.Name;
                    main.SelectedCollection = result.Collection;
                });
            }
        }
    }

    private void FontContextFlyout_Opening(object sender, object e)
    {
        if (sender is MenuFlyout menu && menu.Target.DataContext is CMFontFamily font)
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
            && sender is FrameworkElement { DataContext: CMFontFamily font })
        {
            _ = FontMapView.CreateNewViewForFontAsync(font);
        }
    }
}
