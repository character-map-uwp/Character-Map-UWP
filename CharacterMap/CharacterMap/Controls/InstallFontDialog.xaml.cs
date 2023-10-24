using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

public sealed partial class InstallFontDialog : ContentDialog
{
    private readonly FontAnalysis _analysis;

    public InstallFontDialog(FontAnalysis analysis)
    {
        this.InitializeComponent();
        _analysis = analysis;
    }

    private async void StartClick(object sender, RoutedEventArgs e)
    {
        var path = FontFinder.GetAppPath(_analysis.FilePath).ToLower();
        var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(path));
        var result = await Launcher.LaunchFileAsync(file, new LauncherOptions { DisplayApplicationPicker = true });
    }
}
