using System;
using Windows.ApplicationModel.Core;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace CharacterMap.Helpers
{
    public static class TitleBarHelper
    {
        public static void ExtendTitleBar()
        {
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;
        }

        public static void SetTitleBarColors()
        {
            var accentColor = Edi.UWP.Helpers.UI.GetAccentColor();
            var btnHoverColor = Color.FromArgb(128,
                (byte)(accentColor.R + 30),
                (byte)(accentColor.G + 30),
                (byte)(accentColor.B + 30));
            Edi.UWP.Helpers.UI.ApplyColorToTitleBar(
                accentColor,
                Colors.White,
                Colors.LightGray,
                Colors.Gray);
            Edi.UWP.Helpers.UI.ApplyColorToTitleButton(
                accentColor, Colors.White,
                btnHoverColor, Colors.White,
                accentColor, Colors.White,
                Colors.LightGray, Colors.Gray);
        }

        internal static void SetTitle(string name)
        {
            ApplicationView.GetForCurrentView().Title = name ?? string.Empty;
        }
    }
}
