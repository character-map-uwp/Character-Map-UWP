using System;
using Windows.ApplicationModel.Core;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace CharacterMap.Helpers
{
    public static class TitleBarHelper
    {
        internal static void SetTitle(string name)
        {
            ApplicationView.GetForCurrentView().Title = name ?? string.Empty;
        }
    }
}
