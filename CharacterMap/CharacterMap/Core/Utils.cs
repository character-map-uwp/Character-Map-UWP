using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Svg;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace CharacterMap.Core
{
    public class Utils
    {
        public static CanvasDevice CanvasDevice { get; } = CanvasDevice.GetSharedDevice();

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

        public static bool TryParseHexString(string hexNumber, out int hex)
        {
            hexNumber = hexNumber.Replace("x", string.Empty);
            if (int.TryParse(hexNumber, System.Globalization.NumberStyles.HexNumber, null, out int result))
            {
                hex = result;
                return true;
            }
            hex = 0;
            return false;
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
            double width,
            double height,
            string path)
        {
            return GenerateSvgDocument(device, width, height, new List<string> { path });
        }

        public static CanvasSvgDocument GenerateSvgDocument(
            ICanvasResourceCreator device,
            double width, 
            double height, 
            IList<string> paths)
        {
            width = Math.Ceiling(width);
            height = Math.Ceiling(height);
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("<svg width=\"100%\" height=\"100%\" viewBox=\"0 0 {0} {1}\" xmlns=\"http://www.w3.org/2000/svg\">", width, height);
            foreach (var path in paths)
            {
                sb.AppendFormat("<path d=\"{0}\" />", path);
            }
            sb.Append("</svg>");

            CanvasSvgDocument doc = CanvasSvgDocument.LoadFromXml(device, sb.ToString());

            // TODO : When we export colour SVGs we'll need to set all the correct path fills here

            return doc;
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

    }
}
