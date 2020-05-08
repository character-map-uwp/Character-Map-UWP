using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Text;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using CharacterMap.Core;

namespace CharacterMap.Controls
{
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
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"<!-- If possible, please describe what you were doing before the issue occurred -->");
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("<!-- Please do not edit below this line -->");
            sb.AppendLine($"```\n{ExceptionBlock.Text}\n```");
            sb.AppendLine();
            sb.AppendLine($"**OS Version**: {SystemInformation.OperatingSystemVersion}");
            sb.AppendLine($"**OS Architecture**: {SystemInformation.OperatingSystemArchitecture}");
            sb.AppendLine($"**App Version**: {SystemInformation.ApplicationVersion.ToFormattedString()}");
            sb.AppendLine($"**App Culture**: {SystemInformation.Culture.Name}");

            Utils.CopyToClipBoard(sb.ToString());
            var uri = new Uri("https://github.com/character-map-uwp/Character-Map-UWP/issues/new");
            _ = Launcher.LaunchUriAsync(uri);
        }
    }
}
