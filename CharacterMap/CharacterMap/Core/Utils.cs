using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace CharacterMap.Core
{
    public class Utils
    {
        public static void UseDarkTheme()
        {
            Edi.UWP.Helpers.UI.ApplyColorToTitleBar(Color.FromArgb(255, 43, 43, 43), Colors.White, Colors.DimGray, Colors.White);
            Edi.UWP.Helpers.UI.ApplyColorToTitleButton(Color.FromArgb(255, 43, 43, 43), Colors.White, Colors.DimGray, Colors.White, Colors.DimGray, Colors.White, Colors.DimGray, Colors.White);
            Edi.UWP.Helpers.Mobile.SetWindowsMobileStatusBarColor(Color.FromArgb(255, 43, 43, 43), Colors.DarkGray);
        }

        public static void UseLightTheme()
        {
            var color = Edi.UWP.Helpers.UI.GetAccentColor();

            Edi.UWP.Helpers.Mobile.SetWindowsMobileStatusBarColor(color, Colors.White);
            Edi.UWP.Helpers.UI.ApplyColorToTitleBar(
            color,
            Colors.White,
            Colors.LightGray,
            Colors.Gray);

            Edi.UWP.Helpers.UI.ApplyColorToTitleButton(
                color, Colors.White,
                color, Colors.White,
                color, Colors.White,
                Colors.LightGray, Colors.Gray);
        }
    }
}
