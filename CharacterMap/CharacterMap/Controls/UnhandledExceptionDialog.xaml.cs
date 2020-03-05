using CharacterMap.Helpers;
using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


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
            sb.AppendLine($"```\n{ExceptionBlock.Text}\n```");
            sb.AppendLine();
            sb.AppendLine($"**OS Version**: {SystemInformation.OperatingSystemVersion}");
            sb.AppendLine($"**OS Architecture**: {SystemInformation.OperatingSystemArchitecture}");
            sb.AppendLine($"**App Version**: {SystemInformation.ApplicationVersion.ToFormattedString()}");
            sb.AppendLine($"**App Culture**: {SystemInformation.Culture.Name}");

            Edi.UWP.Helpers.Utils.CopyToClipBoard(sb.ToString());
            var uri = new Uri("https://github.com/EdiWang/Character-Map-UWP/issues/new");
            _ = Launcher.LaunchUriAsync(uri);
        }
    }
}
