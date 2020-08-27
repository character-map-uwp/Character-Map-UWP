using CharacterMap.Models;
using CharacterMap.ViewModels;
using CharacterMapCX;
using GalaSoft.MvvmLight.Ioc;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Svg;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Core
{
    public static class Utils
    {
        public static CanvasDevice CanvasDevice { get; } = CanvasDevice.GetSharedDevice();

        public static NativeInterop GetInterop() => SimpleIoc.Default.GetInstance<NativeInterop>();

        public static void RunOnDispatcher(this DependencyObject d, Action a)
        {
            if (d.Dispatcher.HasThreadAccess)
                a();
            else
                _ = d.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => a());
        }

        public static Color GetAccentColor()
        {
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                return (Color)Application.Current.Resources["SystemAccentColor"];
            }
            return new UISettings().GetColorValue(UIColorType.Accent);
        }

        public static void CopyToClipBoard(string str)
        {
            var dp = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy,
            };
            dp.SetText(str);
            Clipboard.SetContent(dp);
        }

        public static Task<bool> TryCopyToClipboardAsync(Character character, FontMapViewModel viewModel)
        {
            string c = @$"\u{character.UnicodeIndex}?";
            return TryCopyToClipboardInternalAsync(character.Char, c, viewModel);
        }

        public static Task<bool> TryCopyToClipboardAsync(string s, FontMapViewModel viewModel)
        {
            string c = string.Join(string.Empty, s.Select(ch => @$"\u{(uint)ch}?"));
            return TryCopyToClipboardInternalAsync(s, c, viewModel);
        }

        public static async Task<bool> TryCopyToClipboardInternalAsync(string rawString, string formattedString, FontMapViewModel viewModel)
        {
            // Internal helper method to set clipboard
            static void TrySetClipboard(string raw, string formatted, FontMapViewModel v)
            {
                DataPackage dp = new DataPackage
                {
                    RequestedOperation = DataPackageOperation.Copy,
                };

                dp.SetText(raw);

                if (!v.SelectedVariant.IsImported)
                {
                    // We can allow users to also copy the glyph with the font meta-data included,
                    // so when they paste into a supported program like Microsoft Word or 
                    // Adobe Photoshop the correct font is automatically applied to the paste.
                    // This can't include any Typographic variations unfortunately.

                    var rtf = $@"{{\rtf1\fbidis\ansi\ansicpg1252\deff0\nouicompat\deflang2057{{\fonttbl{{\f0\fnil {v.FontFamily.Source};}}}} " +
                               $@"{{\colortbl;\red0\green0\blue0; }}\pard\plain\f0 {formatted}}}";
                    dp.SetRtf(rtf);

                    var longName = v.FontFamily.Source;
                    if (v.SelectedVariant.FontInformation.FirstOrDefault(i => i.Key == "Full Name") is var p)
                    {
                        if (p.Value != longName)
                            longName = $"{v.FontFamily.Source}, {p.Value}";
                    }
                    dp.SetHtmlFormat($"<p style=\"font-family:'{longName}'; \">{raw}</p>");
                }

                Clipboard.SetContent(dp);

                // Possibly causing crashes.
                //Clipboard.Flush();
            }

            // Clipboard can fail when setting. 
            // We will try 5 times. If that fails, we must 
            int i = 0;
            while (i < 5)
            {
                try
                {
                    TrySetClipboard(rawString, formattedString, viewModel);
                    return true;
                }
                catch (Exception ex) when (i < 4)
                {
                    await Task.Delay(150);
                    i++;
                }
            }

            return false;
        }

        public static bool TryParseHexString(string hexNumber, out int hex)
        {
            hexNumber = hexNumber.Replace("x", string.Empty);
            if (int.TryParse(hexNumber, NumberStyles.HexNumber, null, out int result))
            {
                hex = result;
                return true;
            }
            hex = 0;
            return false;
        }

        public static MenuFlyoutPresenter GetPresenter(this MenuFlyout flyout)
        {
            if (flyout.Items.Count == 0)
                return null;

            return flyout.Items[0].GetFirstAncestorOfType<MenuFlyoutPresenter>();
        }

        public static bool IsAccentColorDark()
        {
            var uiSettings = new UISettings();
            var c = uiSettings.GetColorValue(UIColorType.Accent);
            var isDark = (5 * c.G + 2 * c.R + c.B) <= 8 * 128;
            return isDark;
        }

        public static string GetAppDescription()
        {
            var package = Package.Current;
            var packageId = package.Id;
            var version = packageId.Version;
            var architecture = Package.Current.Id.Architecture.ToString();

            return $"{package.DisplayName} - {version.Major}.{version.Minor}.{version.Build}.{version.Revision} ({architecture})";
        }

        public static string GetVariantDescription(CanvasFontFace fontFace)
        {
            StringBuilder s = new StringBuilder();
            s.Append(GetWeightName(fontFace.Weight));

            if (fontFace.Style != FontStyle.Normal)
                s.AppendFormat(", {0}", fontFace.Style);

            if (fontFace.Stretch != FontStretch.Normal)
                s.AppendFormat(", {0}", fontFace.Stretch);

            return s.ToString();
        }

        public static string GetWeightName(FontWeight weight)
        {
            switch (weight.Weight)
            {
                case 100:
                    return nameof(FontWeights.Thin);
                case 150:
                    return "SemiThin";
                case 200:
                    return nameof(FontWeights.ExtraLight);
                case 250:
                    return "SemiExtraLight";
                case 300:
                    return nameof(FontWeights.Light);
                case 350:
                    return nameof(FontWeights.SemiLight);
                case 400:
                    return nameof(FontWeights.Normal);
                case 450:
                    return "SemiMedium";
                case 500:
                    return nameof(FontWeights.Medium);
                case 550:
                    return "ExtraMedium";
                case 600:
                    return nameof(FontWeights.SemiBold);
                case 650:
                    return "ExtraSemiBold";
                case 700:
                    return nameof(FontWeights.Bold);
                case 750:
                    return "SemiExtraBold";
                case 800:
                    return nameof(FontWeights.ExtraBold);
                case 850:
                    return "SemiBlack";
                case 900:
                    return nameof(FontWeights.Black);
                case 950:
                    return nameof(FontWeights.ExtraBlack);
                default:
                    return weight.Weight.ToString();
            }

        }

        public static CanvasSvgDocument GenerateSvgDocument(
            ICanvasResourceCreator device,
            Rect rect,
            string path,
            Color color)
        {
            return GenerateSvgDocument(device, rect, new List<string> { path }, new List<Color> { color });
        }

        public static CanvasSvgDocument GenerateSvgDocument(
            ICanvasResourceCreator device,
            Rect rect, 
            IList<string> paths,
            IList<Color> colors,
            bool invertBounds = true)
        {
            var right = Math.Ceiling(rect.Width);
            var bottom = Math.Ceiling(rect.Height);
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(
                CultureInfo.InvariantCulture, 
                "<svg width=\"100%\" height=\"100%\" viewBox=\"{2} {3} {0} {1}\" xmlns=\"http://www.w3.org/2000/svg\">", 
                right,
                bottom, 
                invertBounds ? -Math.Floor(rect.Left) : Math.Floor(rect.Left),
                invertBounds ? -Math.Floor(rect.Top) : Math.Floor(rect.Top));

            foreach (var path in paths)
            {
                string p = path;
                if (path.StartsWith("F1 "))
                    p = path.Remove(0, 3);

                if (string.IsNullOrWhiteSpace(p))
                    continue;

                sb.AppendFormat("<path d=\"{0}\" style=\"fill: {1}; fill-opacity: {2}\" />",
                    p,
                    AsHex(colors[paths.IndexOf(path)]),
                    (double)colors[paths.IndexOf(path)].A / 255d);
            }
            sb.Append("</svg>");

            CanvasSvgDocument doc = CanvasSvgDocument.LoadFromXml(device, sb.ToString());

            // TODO : When we export colour SVGs we'll need to set all the correct path fills here

            return doc;
        }

        private static string AsHex(Color c)
        {
            return $"#{c.R:x2}{c.G:x2}{c.B:x2}";
        }

        public static Task WriteSvgAsync(CanvasSvgDocument document, IStorageFile file)
        {
            return WriteSvgAsync(document.GetXml(), file);
        }

        public static Task WriteSvgAsync(string xml, IStorageFile file)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<!-- Exported by Character Map UWP -->");
            sb.Append(xml);
            return FileIO.WriteTextAsync(file, sb.ToString()).AsTask();
        }

        public static bool Supports1809 { get; } = ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7);

        public static bool Supports1903 { get; } = ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8);

        public static CanvasTypography GetEffectiveTypography(this TypographyFeatureInfo canvas)
        {
            CanvasTypography typo = new CanvasTypography();
            if (canvas != null && canvas.Feature != CanvasTypographyFeatureName.None)
            {
                typo.AddFeature(canvas.Feature, 1u);
            }
            return typo;
        }
    }
}
