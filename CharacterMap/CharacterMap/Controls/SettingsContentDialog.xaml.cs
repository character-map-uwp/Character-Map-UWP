using CharacterMap.Core;
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
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace CharacterMap.Controls
{
    public sealed partial class SettingsContentDialog : ContentDialog
    {
        public AppSettings Settings { get; }

        public SettingsContentDialog(AppSettings settings)
        {
            Settings = settings;
            this.InitializeComponent();
            this.Closed += SettingsDialog_Closed;
        }

        private void SettingsDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            this.Bindings.StopTracking();
        }
    }
}
