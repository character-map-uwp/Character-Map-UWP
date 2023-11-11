using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

public sealed partial class UnhandledExceptionDialog
{
    public static void Show(Exception ex)
    {
        _ = (new UnhandledExceptionDialog(ex)).ShowAsync();
    }
}

public sealed partial class UnhandledExceptionDialog : ContentDialog
{
    private readonly Exception _ex;

    public UnhandledExceptionDialog(Exception ex)
    {
        _ex = ex;

        this.InitializeComponent();
        ExceptionBlock.Text = $"{ex.Message}\r\n{ex.StackTrace}";
    }

    private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
    {
        StringBuilder sb = new();
        sb.AppendLine($"<!-- If possible, please describe what you were doing before the issue occurred -->");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("<!-- Please do not edit below this line -->");
        sb.AppendLine($"```\n{ExceptionBlock.Text}\n```");
        sb.AppendLine();
        sb.AppendLine($"**OS Version**: {SystemInformation.Instance.OperatingSystemVersion}");
        sb.AppendLine($"**OS Architecture**: {SystemInformation.Instance.OperatingSystemArchitecture}");
        sb.AppendLine($"**App Version**: {SystemInformation.Instance.ApplicationVersion.ToFormattedString()}");
        sb.AppendLine($"**App Culture**: {SystemInformation.Instance.Culture.Name}");

        Utils.CopyToClipBoard(sb.ToString());
        Uri uri = new("https://github.com/character-map-uwp/Character-Map-UWP/issues/new");
        _ = Launcher.LaunchUriAsync(uri);
    }
}
