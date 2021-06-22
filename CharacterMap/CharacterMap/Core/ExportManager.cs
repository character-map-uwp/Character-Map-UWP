using CharacterMap.Helpers;
using CharacterMap.Services;
using CharacterMapCX;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Svg;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;
using CharacterMap.Models;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Messaging;
using System.Threading;

namespace CharacterMap.Core
{
    public enum ExportFormat : int
    { 
        Png = 0,
        Svg = 1
    }

    public enum ExportStyle
    {
        Black,
        White,
        ColorGlyph
    }

    public record ExportOptions
    {
        public double PreferredSize { get; init; }
        public ExportFormat PreferredFormat { get; init; }
        public ExportStyle PreferredStyle { get; init; }
        public Color PreferredColor { get; init; }
        public StorageFolder TargetFolder { get; init; }

        public ExportOptions() { }

        public ExportOptions(ExportFormat format, ExportStyle style)
        {
            PreferredSize = ResourceHelper.AppSettings.PngSize;
            PreferredFormat = format;
            PreferredColor = style switch
            {
                ExportStyle.White => Colors.White,
                _ => Colors.Black
            };
            PreferredStyle = style;
        }

    }

    public class ExportResult
    {
        public StorageFile File { get; }
        public bool Success { get; }

        public ExportResult(bool success, StorageFile file)
        {
            Success = success;
            File = file;
        }
    }

    public class ExportFontFileResult
    {
        public StorageFolder Folder { get; }
        public StorageFile File { get; }
        public bool Success { get; }

        public ExportFontFileResult(bool success, StorageFile file)
        {
            Success = success;
            File = file;
        }

        public ExportFontFileResult(StorageFolder folder, bool success)
        {
            Success = success;
            Folder = folder;
        }

        public string GetMessage()
        {
            if (Folder != null)
                return Localization.Get("ExportedToFolderMessage", Folder.Name);
            else
                return Localization.Get("FontExportedMessage", File.Name);
        }
    }

    public static partial class ExportManager
    {
        public static string GetSVG(
            ExportStyle style,
            Color textColor,
            CharacterRenderingOptions options,
            Character selectedChar)
        {
            // We want to prepare geometry at 1024px, so force this
            options = options with { FontSize = 1024 };
            using var typography = options.CreateCanvasTypography();

            CanvasDevice device = Utils.CanvasDevice;

            // If COLR format (e.g. Segoe UI Emoji), we have special export path.
            if (style == ExportStyle.ColorGlyph && options.Analysis.HasColorGlyphs && !options.Analysis.GlyphFormats.Contains(GlyphImageFormat.Svg))
            {
                NativeInterop interop = Utils.GetInterop();
                List<string> paths = new List<string>();
                Rect bounds = Rect.Empty;

                // Try to find the bounding box of all glyph layers combined
                foreach (var thing in options.Analysis.Indicies)
                {
                    var path = interop.GetPathDatas(options.Variant.FontFace, thing.ToArray()).First();
                    paths.Add(path.Path);

                    if (!path.Bounds.IsEmpty)
                    {
                        var left = Math.Min(bounds.Left, path.Bounds.Left);
                        var top = Math.Min(bounds.Top, path.Bounds.Top);
                        var right = Math.Max(bounds.Right, path.Bounds.Right);
                        var bottom = Math.Max(bounds.Bottom, path.Bounds.Bottom);
                        bounds = new Rect(
                            left,
                            top,
                            right - left,
                            bottom - top);
                    }
                }

                using CanvasSvgDocument document = Utils.GenerateSvgDocument(device, bounds, paths, options.Analysis.Colors, invertBounds: false);
                return document.GetXml();
            }

            var data = GetGeometry(selectedChar, options);
            string GetMonochrome()
            {
                using CanvasSvgDocument document = string.IsNullOrWhiteSpace(data.Path) 
                    ? new CanvasSvgDocument(Utils.CanvasDevice)
                    : Utils.GenerateSvgDocument(device, data.Bounds, data.Path, textColor);
                return document.GetXml();
            }

            // If the font uses SVG glyphs, we can extract the raw SVG from the font file
            if (options.Analysis.GlyphFormats.Contains(GlyphImageFormat.Svg))
            {
                string str = null;
                IBuffer b = GetGlyphBuffer(options.Variant.FontFace, selectedChar.UnicodeIndex, GlyphImageFormat.Svg);

                // If the SVG glyph is compressed we need to decompress it
                if (b.Length > 2 && b.GetByte(0) == 31 && b.GetByte(1) == 139)
                {
                    using var stream = b.AsStream();
                    using var gzip = new GZipStream(stream, CompressionMode.Decompress);
                    using var reader = new StreamReader(gzip);
                    str = reader.ReadToEnd();
                }
                else
                {
                    using var dataReader = DataReader.FromBuffer(b);
                    dataReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                    str = dataReader.ReadString(b.Length);
                }

                // CanvasSvgDocument doesn't like the <?xml ... /> tag, so remove it
                if (str.StartsWith("<?xml"))
                    str = str.Remove(0, str.IndexOf(">") + 1);

                str = str.TrimStart();

                try
                {
                    using (CanvasSvgDocument document = CanvasSvgDocument.LoadFromXml(Utils.CanvasDevice, str))
                    {
                        // We need to transform the SVG to fit within the default document bounds, as characters
                        // are based *above* the base origin of (0,0) as (0,0) is the Baseline (bottom left) position for a character, 
                        // so by default a will appear out of bounds of the default SVG viewport (towards top left).

                        //if (!document.Root.IsAttributeSpecified("viewBox")) // Specified viewbox requires baseline transform?
                        {
                            // We'll regroup all the elements inside a "g" / group tag,
                            // and apply a transform to the "g" tag to try and put in 
                            // in the correct place. There's probably a more accurate way
                            // to do this by directly setting the root viewBox, if anyone
                            // can find the correct calculation...

                            List<ICanvasSvgElement> elements = new List<ICanvasSvgElement>();

                            double minTop = 0;
                            double minLeft = double.MaxValue;
                            double maxWidth = double.MinValue;
                            double maxHeight = double.MinValue;

                            void ProcessChildren(CanvasSvgNamedElement root)
                            {
                                CanvasSvgNamedElement ele = root.FirstChild as CanvasSvgNamedElement;
                                while (true)
                                {
                                    CanvasSvgNamedElement next = root.GetNextSibling(ele) as CanvasSvgNamedElement;
                                    if (ele.Tag == "g")
                                    {
                                        ProcessChildren(ele);
                                    }
                                    else if (ele.Tag == "path")
                                    {
                                        // Create a XAML geometry to try and find the bounds of each character
                                        // Probably more efficient to do in Win2D, but far less code to do with XAML.
                                        Geometry gm = XamlBindingHelper.ConvertValue(typeof(Geometry), ele.GetStringAttribute("d")) as Geometry;
                                        minTop = Math.Min(minTop, gm.Bounds.Top);
                                        minLeft = Math.Min(minLeft, gm.Bounds.Left);
                                        maxWidth = Math.Max(maxWidth, gm.Bounds.Width);
                                        maxHeight = Math.Max(maxHeight, gm.Bounds.Height);
                                    }
                                    ele = next;
                                    if (ele == null)
                                        break;
                                }
                            }

                            ProcessChildren(document.Root);

                            double top = minTop < 0 ? minTop : 0;
                            double left = minLeft;
                            document.Root.SetRectangleAttribute("viewBox", new Rect(left, top, data.Bounds.Width, data.Bounds.Height));
                        }

                        return document.GetXml();
                    }
                }
                catch
                {
                    // Certain fonts seem to have their SVG glyphs encoded with... I don't even know what encoding.
                    // for example: https://github.com/adobe-fonts/emojione-color
                    // In these cases, fallback to monochrome black
                    return GetMonochrome();
                }
            }
            else
            {
                return GetMonochrome();
            }
        }

        public static Task<ExportResult> ExportGlyphAsync(
            ExportOptions export,
            InstalledFont selectedFont,
            CharacterRenderingOptions options,
            Character selectedChar,
            StorageFolder targetFolder = null)
        {
            if (export.PreferredFormat == ExportFormat.Png)
                return ExportPngAsync(export, selectedFont, options, selectedChar, ResourceHelper.AppSettings, targetFolder);
            else
                return ExportSvgAsync(export, selectedFont, options, selectedChar, targetFolder);
        }

        public static async Task<ExportResult> ExportSvgAsync(
            ExportOptions style,
            InstalledFont selectedFont,
            CharacterRenderingOptions options,
            Character selectedChar,
            StorageFolder targetFolder = null)
        {
            try
            {
                // 1. Get the file we will save the image to.
                string name = GetFileName(selectedFont, options.Variant, selectedChar, "svg");
                StorageFile file = null;
                if (targetFolder != null)
                    file = await targetFolder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting);
                else
                    file = await PickFileAsync(name, "SVG", new[] { ".svg" });

                if (file is not null)
                {
                    try
                    {
                        // We want to prepare geometry at 1024px, so force this
                        options = options with { FontSize = 1024 };
                        using var typography = options.CreateCanvasTypography();

                        CachedFileManager.DeferUpdates(file);

                        // Generate SVG doc and write to file
                        await Utils.WriteSvgAsync(GetSVG(style.PreferredStyle, style.PreferredColor, options, selectedChar), file);
                        return new ExportResult(true, file);
                    }
                    finally
                    {
                        await CachedFileManager.CompleteUpdatesAsync(file);
                    }
                }
            }
            catch (Exception ex)
            {
                if (targetFolder is null)
                    await Ioc.Default.GetService<IDialogService>()
                        .ShowMessageAsync(ex.Message, Localization.Get("SaveImageError"));
            }

            return new ExportResult(false, null);
        }

        public static async Task<ExportResult> ExportPngAsync(
            ExportOptions style,
            InstalledFont selectedFont,
            CharacterRenderingOptions options,
            Character selectedChar,
            AppSettings settings,
            StorageFolder targetFolder = null)
        {
            try
            {
                // 1. Get the file we will save the image to.
                string name = GetFileName(selectedFont, options.Variant, selectedChar, "png");
                StorageFile file = null;
                if (targetFolder != null)
                    file = await targetFolder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting);
                else
                    file = await PickFileAsync(name, "PNG Image", new[] { ".png" });


                if (file is not null)
                {
                    CachedFileManager.DeferUpdates(file);
                    using var typography = options.CreateCanvasTypography();

                    // If the glyph is actually a PNG file inside the font we should export it directly.
                    // TODO : We're not actually exporting with typography options here.
                    //        Find a test PNG font with typography
                    if (options.Analysis.GlyphFormats.Contains(GlyphImageFormat.Png))
                    {
                        IBuffer buffer = GetGlyphBuffer(options.Variant.FontFace, selectedChar.UnicodeIndex, GlyphImageFormat.Png);
                        await FileIO.WriteBufferAsync(file, buffer);
                    }
                    else
                    {
                        var device = Utils.CanvasDevice;
                        var localDpi = 96; //Windows.Graphics.Display.DisplayInformation.GetForCurrentView().LogicalDpi;

                        float size = style.PreferredSize > 0 ? (float)style.PreferredSize : (float)settings.PngSize;
                        var canvasH = size;
                        var canvasW = size;

                        using var renderTarget = new CanvasRenderTarget(device, canvasW, canvasH, localDpi);
                        using (var ds = renderTarget.CreateDrawingSession())
                        {
                            ds.Clear(Colors.Transparent);
                            var d = settings.PngSize;
                            var r = settings.PngSize / 2;

                            var textColor = style.PreferredColor;
                            var fontSize = (float)d;

                            using CanvasTextLayout layout = new (device, $"{selectedChar.Char}", new ()
                            {
                                FontSize = fontSize,
                                FontFamily = options.Variant.Source,
                                FontStretch = options.Variant.FontFace.Stretch,
                                FontWeight = options.Variant.FontFace.Weight,
                                FontStyle = options.Variant.FontFace.Style,
                                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                                Options = style.PreferredStyle == ExportStyle.ColorGlyph ? CanvasDrawTextOptions.EnableColorFont : CanvasDrawTextOptions.Default
                            }, canvasW, canvasH);

                            if (style.PreferredStyle == ExportStyle.ColorGlyph)
                                layout.Options = CanvasDrawTextOptions.EnableColorFont;

                            layout.SetTypography(0, 1, typography);

                            var db = layout.DrawBounds;
                            double scale = Math.Min(1, Math.Min(canvasW / db.Width, canvasH / db.Height));
                            var x = -db.Left + ((canvasW - (db.Width * scale)) / 2d);
                            var y = -db.Top + ((canvasH - (db.Height * scale)) / 2d);

                            ds.Transform =
                                Matrix3x2.CreateTranslation(new Vector2((float)x, (float)y))
                                * Matrix3x2.CreateScale(new Vector2((float)scale));

                            ds.DrawTextLayout(layout, new Vector2(0), textColor);
                        }

                        using var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite);
                        fileStream.Size = 0;
                        await renderTarget.SaveAsync(fileStream, CanvasBitmapFileFormat.Png, 1f);
                    }

                    await CachedFileManager.CompleteUpdatesAsync(file);
                    return new ExportResult(true, file);
                }
            }
            catch (Exception ex)
            {
                if (targetFolder is null)
                    await Ioc.Default.GetService<IDialogService>()
                        .ShowMessageAsync(ex.Message, Localization.Get("SaveImageError"));
            }

            return new ExportResult(false, null);
        }

        private static IBuffer GetGlyphBuffer(CanvasFontFace fontface, uint unicodeIndex, GlyphImageFormat format)
        {
            return DirectWrite.GetImageDataBuffer(fontface, 1024, unicodeIndex, format);
        }

        private static string GetFileName(
            InstalledFont selectedFont,
            FontVariant selectedVariant,
            Character selectedChar,
            string ext)
        {
            var chr = selectedVariant.GetDescription(selectedChar) ?? selectedChar.UnicodeString;
            return $"{selectedFont.Name} {selectedVariant.PreferredName} - {chr}.{ext}";
        }

        private static async Task<StorageFile> PickFileAsync(string fileName, string key, IList<string> values, PickerLocationId suggestedLocation = PickerLocationId.PicturesLibrary)
        {
            var savePicker = new FileSavePicker
            {
                SuggestedStartLocation = suggestedLocation
            };

            savePicker.FileTypeChoices.Add(key, values);
            savePicker.SuggestedFileName = fileName;

            try
            {
                return await savePicker.PickSaveFileAsync();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static (string Path, Rect Bounds) GetGeometry(
            Character selectedChar,
            CharacterRenderingOptions options)
        {
            /* 
             * Note: this only constructs the monochrome version
             * of the glyph.
             * 
             * Drop into C++/CX for color / multi-variant glyphs.
             */

            using CanvasGeometry geom = CreateGeometry(selectedChar, options);
            var bounds = geom.ComputeBounds();
            var interop = Utils.GetInterop();
            var s = interop.GetPathData(geom);

            if (string.IsNullOrWhiteSpace(s.Path))
                return (s.Path, bounds);

            var t = s.Transform.Translation;
            bounds = new Rect(t.X - bounds.Left, -bounds.Top + t.Y, bounds.Width, bounds.Height);
            return (s.Path, bounds);
        }

        public static CanvasGeometry CreateGeometry(
           Character selectedChar,
           CharacterRenderingOptions options)
        {
            CanvasDevice device = Utils.CanvasDevice;

            /* SVG Exports render at fixed size - but a) they're vectors, and b) they're
             * inside an auto-scaling viewport. So render-size is *largely* pointless */
            float canvasH = options.FontSize, canvasW = options.FontSize, fontSize = options.FontSize;

            using var typography = options.CreateCanvasTypography();
            using (CanvasTextLayout layout = new (device, $"{selectedChar.Char}", new ()
            {
                FontSize = fontSize,
                FontFamily = options.Variant.Source,
                FontStretch = options.Variant.FontFace.Stretch,
                FontWeight = options.Variant.FontFace.Weight,
                FontStyle = options.Variant.FontFace.Style,
                HorizontalAlignment = CanvasHorizontalAlignment.Center
            }, canvasW, canvasH))
            {
                layout.SetTypography(0, 1, typography);
                layout.Options = options.Analysis.GlyphFormats.Contains(GlyphImageFormat.Svg) ? CanvasDrawTextOptions.EnableColorFont : CanvasDrawTextOptions.Default;

                return CanvasGeometry.CreateText(layout);
            }
        }

        private static IAsyncOperation<StorageFolder> PickFolderAsync()
        {
            FolderPicker picker = new ()
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add("*");

            return picker.PickSingleFolderAsync();
        }

        internal static async Task ExportFontToFolderAsync(
            InstalledFont family, 
            CharacterRenderingOptions options, 
            IReadOnlyList<Character> characters,
            ExportOptions opts,
            Action<int, int> callback)
        {
            if (await PickFolderAsync() is StorageFolder folder)
            {
                List<ExportResult> fails = new();

                int i = 0;
                foreach (var c in characters)
                {
                    i++;

                    callback?.Invoke(i, characters.Count);

                    ExportResult result = await ExportGlyphAsync(opts, family, options, c, folder).ConfigureAwait(false);
                    if (result is not null && result.Success is false)
                        fails.Add(result);
                }

                WeakReferenceMessenger.Default.Send(
                    new AppNotificationMessage(true, new ExportFontFileResult(folder, true)));
            }
        }
    }
}
