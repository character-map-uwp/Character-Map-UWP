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
            var titleBar = ApplicationView.GetForCurrentView().TitleBar;

            Color hoverColor = Colors.Transparent;
            Color pressedColor = Colors.Transparent;
            Color foregroundColor = Colors.Transparent;

            if (App.Current.RequestedTheme == Windows.UI.Xaml.ApplicationTheme.Dark)
            {
                hoverColor = Color.FromArgb(25, 255, 255, 255);
                pressedColor = Color.FromArgb(51, 255, 255, 255);
                foregroundColor = Color.FromArgb(255, 255, 255, 255);
            }
            else
            {
                hoverColor = Color.FromArgb(25, 0, 0, 0);
                pressedColor = Color.FromArgb(51, 0, 0, 0);
                foregroundColor = Color.FromArgb(255, 0, 0, 0);
            }

            // Set active window colors
            titleBar.ForegroundColor = foregroundColor;
            titleBar.BackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = foregroundColor;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonHoverForegroundColor = foregroundColor;
            titleBar.ButtonHoverBackgroundColor = hoverColor;
            titleBar.ButtonPressedForegroundColor = foregroundColor;
            titleBar.ButtonPressedBackgroundColor = pressedColor;

            // Set inactive window colors
            titleBar.InactiveForegroundColor = Colors.Gray;
            titleBar.InactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveForegroundColor = Colors.Gray;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }
    }
}
