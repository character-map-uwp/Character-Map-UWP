using CharacterMap.Models;
using CharacterMap.ViewModels;
using CharacterMapCX;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Svg;
using Microsoft.Graphics.Canvas.Text;
using CommunityToolkit.Mvvm.DependencyInjection;
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
using Windows.UI.Xaml.Controls.Primitives;
using Windows.Storage.Streams;
using System.IO;
using Windows.System;
using Windows.UI.Core;

namespace CharacterMap.Core
{
    public class Pool<T> where T : new()
    {
        Queue<T> _pool { get; } = new ();

        public T Request()
        {
            if (_pool.TryDequeue(out T item))
                return item;

            return new();
        }

        public void Return(T value)
        {
            _pool.Enqueue(value);
        }
    }

    public static class Utils
    {
        public static CanvasDevice CanvasDevice { get; } = CanvasDevice.GetSharedDevice();

        public static NativeInterop GetInterop() => Ioc.Default.GetService<NativeInterop>();

        public static void RunOnDispatcher(this DependencyObject d, Action a)
        {
            if (d.Dispatcher.HasThreadAccess)
                a();
            else
                _ = d.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => a());
        }

        public static void ToggleFullScreenMode()
        {
            var view = ApplicationView.GetForCurrentView();
            if (view.IsFullScreenMode)
                view.ExitFullScreenMode();
            else
                view.TryEnterFullScreenMode();
        }

        public static bool IsKeyDown(VirtualKey key)
        {
            var state = CoreWindow.GetForCurrentThread().GetKeyState(key);
            return (state & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
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
            DataPackage dp = new() { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(str);
            Clipboard.SetContent(dp);
        }

        public static async Task<bool> TryCopyToClipboardAsync(CopyToClipboardMessage msg, FontMapViewModel viewModel)
        {
            string c = @$"\u{msg.RequestedItem.UnicodeIndex}?";

            if (msg.DataType == CopyDataType.Text)
            {
                return await TryCopyToClipboardInternalAsync(msg.RequestedItem.Char, c, viewModel);
            }
            else if (msg.DataType == CopyDataType.SVG)
            {
                ExportOptions ops = new(ExportFormat.Svg, msg.Style);
                var svg = ExportManager.GetSVG(msg.Style, ops.PreferredColor, viewModel.RenderingOptions, msg.RequestedItem);
                return await TryCopyToClipboardInternalAsync(svg, c, viewModel, msg.DataType);
            }
            else if (msg.DataType == CopyDataType.PNG)
            {
                ExportOptions ops = new(ExportFormat.Png, msg.Style);
                IRandomAccessStream data = await ExportManager.GetGlyphPNGStreamAsync(ops, viewModel.RenderingOptions, msg.RequestedItem);
                return await TryCopyToClipboardInternalAsync(null, c, viewModel, msg.DataType, data);
            }
            else
                return false;
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

        public static async Task<bool> TryCopyToClipboardInternalAsync(string rawString, string formattedString, FontMapViewModel viewModel, CopyDataType type = CopyDataType.Text, IRandomAccessStream data = null)
        {
            // Internal helper method to set clipboard
            static void TrySetClipboard(string raw, string formatted, FontMapViewModel v, CopyDataType copyType, IRandomAccessStream stream = null)
            {
                DataPackage dp = new () {  RequestedOperation = DataPackageOperation.Copy };

                if (raw != null)
                    dp.SetText(raw);

                if (copyType == CopyDataType.SVG)
                    AddSVGToPackage(raw, dp);
                else if (copyType == CopyDataType.PNG)
                {
                    dp.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
                    dp.SetData("image/png", stream);
                    dp.SetData("PNG", stream);
                }
                else if (!v.SelectedVariant.IsImported)
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
                    TrySetClipboard(rawString, formattedString, viewModel, type, data);
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

        private static void AddSVGToPackage(string raw, DataPackage dp)
        {
            InMemoryRandomAccessStream svgData = new();
            StreamWriter writer = new(svgData.AsStream(), Encoding.UTF8, 1024, true);
            writer.Write(raw);
            writer.Flush();
            dp.SetData("image/svg+xml", svgData);
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

        private static string AsHex(this Color c)
        {
            return $"#{c.R:x2}{c.G:x2}{c.B:x2}";
        }

        /// <summary>
        /// Returns a string attempting to show only characters a font supports.
        /// Unsupported characters are replaced with the Unicode replacement character.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string GetSafeString(CanvasFontFace fontFace, string s)
        {
            /* 
             * Ideally we actually want to use DirectTextBlock
             * instead of TextBlock to get correct display of 
             * Fallback characters, but there is some bug preventing
             * rendering I can't figure out, so this is our hack for
             * now.
             */

            string r = string.Empty;
            if (s != null && fontFace != null)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    var c = s[i];

                    /* Surrogate pair handling is pain */
                    if (char.IsSurrogate(c)
                        && char.IsSurrogatePair(c, s[i + 1]))
                    {
                        var c1 = s[i + 1];
                        int val = char.ConvertToUtf32(c, c1);
                        if (fontFace.HasCharacter((uint)val))
                            r += new string(new char[] { c, c1 });
                        else
                            r += '\uFFFD';

                        i += 1;
                    }
                    else if (fontFace.HasCharacter(c))
                        r += c;
                    else
                        r += '\uFFFD';
                }

            }

            return r;
        }

        public static FrameworkElement GetPresenter(this FlyoutBase flyout)
        {
            return flyout switch
            {
                Flyout f => f.GetPresenter(),
                MenuFlyout m => m.GetPresenter(),
                _ => null
            };
        }

        public static FlyoutPresenter GetPresenter(this Flyout flyout)
        {
            return flyout.Content?.GetFirstAncestorOfType<FlyoutPresenter>();
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

        public static string GetVariantDescription(DWriteFontFace fontFace)
        {
            StringBuilder s = new StringBuilder();
            s.Append(GetWeightName(fontFace.Properties.Weight));

            if (fontFace.Properties.Style != FontStyle.Normal)
                s.AppendFormat(", {0}", fontFace.Properties.Style);

            if (fontFace.Properties.Stretch != FontStretch.Normal)
                s.AppendFormat(", {0}", fontFace.Properties.Stretch);

            return s.ToString();
        }

        public static string Humanise(this Enum e)
        {
            return Humanise(e.ToString(), true);
        }

        static Pool<StringBuilder> _builderPool { get; } = new Pool<StringBuilder>();

        /// <summary>
        /// Not thread safe.
        /// </summary>
        public static string Humanise(string input, bool title)
        {
            var sb = _builderPool.Request();

            try
            {
                char prev = char.MinValue;
                for (int i = 0; i < input.Length; i++)
                {
                    char c = input[i];

                    if ((char.IsLower(prev) && char.IsUpper(c))
                        || ((char.IsPunctuation(c) || char.IsSeparator(c)) && c != ')')
                        || (sb.Length > 0 && char.IsDigit(c) && !char.IsDigit(prev))
                        || (char.IsDigit(prev) && char.IsLetter(c)))
                    {
                        if (c != 32)
                            sb.Append(' ');

                        if (!title)
                            c = char.ToLowerInvariant(c);
                    }

                    sb.Append(c);
                    prev = c;
                }

                return sb.ToString();
            }
            finally
            {
                sb.Clear();
                _builderPool.Return(sb);
            }
        }

        public static string GetWeightName(FontWeight weight)
        {
            return weight.Weight switch
            {
                100 => nameof(FontWeights.Thin),
                150 => "SemiThin",
                200 => nameof(FontWeights.ExtraLight),
                250 => "SemiExtraLight",
                300 => nameof(FontWeights.Light),
                350 => nameof(FontWeights.SemiLight),
                400 => nameof(FontWeights.Normal),
                450 => "SemiMedium",
                500 => nameof(FontWeights.Medium),
                550 => "ExtraMedium",
                600 => nameof(FontWeights.SemiBold),
                650 => "ExtraSemiBold",
                700 => nameof(FontWeights.Bold),
                750 => "SemiExtraBold",
                800 => nameof(FontWeights.ExtraBold),
                850 => "SemiBlack",
                900 => nameof(FontWeights.Black),
                950 => nameof(FontWeights.ExtraBlack),
                _ => weight.Weight.ToString(),
            };
        }

        public static CanvasSvgDocument GenerateSvgDocument(
            ICanvasResourceCreator device,
            Rect rect,
            string path,
            Color color)
        {
            return GenerateSvgDocument(device, rect, new List<string> { path }, new List<Color> { color });
        }

        /// <summary>
        /// Generates an SVG document for multi-layered glyphs, where each layer has separate colours.
        /// COLR glyphs are an example of this.
        /// </summary>
        /// <param name="device"></param>
        /// <param name="rect">Bounding rectangle of all glyphs</param>
        /// <param name="paths">Geometry of each layer</param>
        /// <param name="colors">Colour of each layer</param>
        /// <param name="invertBounds"></param>
        /// <returns></returns>
        public static CanvasSvgDocument GenerateSvgDocument(
            ICanvasResourceCreator device,
            Rect rect, 
            IList<string> paths,
            IList<Color> colors,
            bool invertBounds = true)
        {
            var right = Math.Ceiling(rect.Width);
            var bottom = Math.Ceiling(rect.Height);
            StringBuilder sb = _builderPool.Request();

            try
            {
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
                        colors[paths.IndexOf(path)].AsHex(),
                        (double)colors[paths.IndexOf(path)].A / 255d);
                }
                sb.Append("</svg>");

                CanvasSvgDocument doc = CanvasSvgDocument.LoadFromXml(device, sb.ToString());
                return doc;
            }
            finally
            {
                sb.Clear();
                _builderPool.Return(sb);
            }
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

        public static async Task DeleteAsync(this StorageFolder folder, bool deleteFolder = false)
        {
            // 1. Delete all child folders
            var folders = await folder.GetFoldersAsync().AsTask().ConfigureAwait(false);
            if (folders.Count > 0)
            {
                var tasks = folders.Select(f => DeleteAsync(f, true));
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            // 2. Delete child files
            var files = await folder.GetFilesAsync().AsTask().ConfigureAwait(false);
            if (files.Count > 0)
            {
                var tasks = files.Select(f => f.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask());
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            // 3. Delete folder
            if (deleteFolder)
                await folder.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
        }

        public static bool Supports1809 { get; } = ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7);

        public static bool Supports1903 { get; } = ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8);
    }
}
