using CharacterMap.Services;
using CommonServiceLocator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Core
{
    public static class Converters
    {
        public static bool False(bool b) => !b;
        public static bool FalseFalse(bool b, bool c) => !b && !c;
        public static bool True(bool b) => b;

        public static Visibility InvertVis(Visibility b) => b == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
        public static Visibility FalseToVis(bool b) => !b ? Visibility.Visible : Visibility.Collapsed;
        public static Visibility TrueToVis(bool b) => b ? Visibility.Visible : Visibility.Collapsed;

        public static bool IsNull(object obj) => obj == null;
        public static bool IsNotNull(object obj) => obj != null;

        public static char ToHex(int i) => (char)i;

        public static string GetWeightName(Windows.UI.Text.FontWeight weight)
        {
            return $"{Utils.GetWeightName(weight)} - {weight.Weight}";
        }

        public static GridLength GridLengthAorB(bool input, string a, string b) 
            => input ? ReadFromString(a) : ReadFromString(b);

        private static GridLength ReadFromString(string s)
        {
            if (s == Auto)
                return new GridLength(1, GridUnitType.Auto);
            else if (s == Star)
                return new GridLength(1, GridUnitType.Star);
            else
                return new GridLength(double.Parse(s), GridUnitType.Pixel);
        }

        public const string Auto = "Auto";
        public const string Star = "*";

        private static AppSettings _settings;
        private static UserCollectionsService _userCollections;

        public static FontFamily GetPreviewFontSource(FontVariant variant)
        {
            if (_settings == null)
            {
                _settings = (AppSettings)App.Current.Resources[nameof(AppSettings)];
                _userCollections = ServiceLocator.Current.GetInstance<UserCollectionsService>();
            }

            if (_settings.UseFontForPreview
                && !variant.FontFace.IsSymbolFont
                && !_userCollections.SymbolCollection.Fonts.Contains(variant.FamilyName))
                return new FontFamily(variant.Source);

            return FontFamily.XamlAutoFontFamily;
        }

        public static string GetFileSize(int fileSize)
        {
            double size = (double)fileSize / 1024;
            if (size < 600)
                return $"{size:0.00} KB";

            size = size / 1024;
            return $"{size:0.00} MB";
        }
    }
}
