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
using CommunityToolkit.Mvvm.DependencyInjection;
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
        public bool SkipEmptyGlyphs { get; init; }

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

    public enum ExportState
    {
        Skipped,
        Succeeded,
        Failed
    }

    public class ExportResult
    {
        public StorageFile File { get; }
        public ExportState State { get; }

        public ExportResult(ExportState state, StorageFile file)
        {
            State = state;
            File = file;
        }

        public static ExportResult CreatedFailed()
        {
            return new ExportResult(ExportState.Failed, null);
        }
    }

    public class ExportGlyphsResult
    {
        public StorageFolder Folder { get; }
        public int Failed { get; }
        public int Skipped { get; }
        public bool Success { get; }
        public int Count { get; }

        public ExportGlyphsResult(bool success, int count, StorageFolder folder, int failed, int skipped)
        {
            Success = success;
            Folder = folder;
            Failed = failed;
            Skipped = skipped;
            Count = count;
        }

        public string GetMessage()
        {
            return Localization.Get("ExportGlyphsResultMessage/Text", Count);
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
            Character selectedChar,
            bool skipEmpty = false)
        {
            // We want to prepare geometry at 1024px, so force this
            options = options with { FontSize = 1024 };
            using var typography = options.CreateCanvasTypography();

            CanvasDevice device = Utils.CanvasDevice;

            // If COLR format (e.g. Segoe UI Emoji), we have special export path.
            // This path does not require UI thread.
            if (style == ExportStyle.ColorGlyph 
                && options.Analysis.HasColorGlyphs 
                && !options.Analysis.GlyphFormats.Contains(GlyphImageFormat.Svg))
            {
                NativeInterop interop = Utils.GetInterop();
                List<string> paths = new ();
                Rect bounds = Rect.Empty;

                // Try to find the bounding box of all glyph layers combined
                foreach (var thing in options.Analysis.Indicies)
                {
                    var path = interop.GetPathDatas(options.Variant.Face, thing.ToArray()).First();
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

            if (string.IsNullOrWhiteSpace(data.Path) && skipEmpty)
                return null;

            string GetMonochrome()
            {
                using CanvasSvgDocument document = string.IsNullOrWhiteSpace(data.Path) 
                    ? new CanvasSvgDocument(Utils.CanvasDevice)
                    : Utils.GenerateSvgDocument(device, data.Bounds, data.Path, textColor);
                return document.GetXml();
            }

            // If the font uses SVG glyphs, we can extract the raw SVG from the font file.
            // This path requires access to the UI thread.
            if (options.Analysis.GlyphFormats.Contains(GlyphImageFormat.Svg))
            {
                string str = null;
                IBuffer b = GetGlyphBuffer(options.Variant.Face, selectedChar.UnicodeIndex, GlyphImageFormat.Svg);

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
                                        // TODO: This forces us to have UI thread access during export. Investigate
                                        //       another solution to this to allow us to go into the background.
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
                catch (Exception ex)
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
            // To export a glyph as an SVG, it must be fully vector based.
            // If it is not, we force export as PNG regardless of choice.
            if (export.PreferredFormat == ExportFormat.Png || options.Analysis.IsFullVectorBased is false)
                return ExportPngAsync(export, selectedFont, options, selectedChar, ResourceHelper.AppSettings, targetFolder);
            else
                // NOTE: SVG Export may require UI thread
                return ExportSvgAsync(export, selectedFont, options, selectedChar, targetFolder);
        }

        public static Task<StorageFile> GetTargetFileAsync(InstalledFont font, FontVariant variant, Character c, string format, StorageFolder targetFolder)
        {
            string name = GetFileName(font, variant, c, format);
            if (targetFolder != null)
                return targetFolder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting).AsTask();
            else
                return PickFileAsync(name, format.ToUpper(), new[] { $".{format}" });
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
                // 0. We want to prepare geometry at 1024px, so force this
                options = options with { FontSize = 1024 };
                using var typography = options.CreateCanvasTypography();

                // 1. Check if we should actually save the file.
                //    Certain export modes will skip blank geometries
                string svg = GetSVG(style.PreferredStyle, style.PreferredColor, options, selectedChar, style.SkipEmptyGlyphs);
                if (string.IsNullOrWhiteSpace(svg) && style.SkipEmptyGlyphs)
                    return new ExportResult(ExportState.Skipped, null);

                // 2. Get the file we will save the image to.
                var providedFile = await GetTargetFileAsync(selectedFont, options.Variant, selectedChar, "svg", targetFolder);                
                if (providedFile is StorageFile file)
                {
                    try
                    {
                        // 3. Write the SVG to the file
                        await Utils.WriteSvgAsync(svg, file);
                        return new ExportResult(ExportState.Succeeded, file);
                    }
                    finally
                    {

                    }
                }
            }
            catch (Exception ex)
            {
                if (targetFolder is null)
                    await Ioc.Default.GetService<IDialogService>()
                        .ShowMessageAsync(ex.Message, Localization.Get("SaveImageError"));
            }

            return new ExportResult(ExportState.Failed, null);
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
                IRandomAccessStream stream = null;
                try
                {
                    // 1. Try to get the glyph data
                    stream = await GetGlyphPNGStreamAsync(style, options, selectedChar);
                    if (stream is null)
                        return new ExportResult(ExportState.Skipped, null);

                    // 2. Get the file we will save the image to.
                    if (await GetTargetFileAsync(selectedFont, options.Variant, selectedChar, "png", targetFolder)
                        is StorageFile file)
                    {
                        // 3. Write to the file
                        using var fileStream = await file.OpenStreamForWriteAsync();
                        fileStream.SetLength(0);
                        await stream.AsStreamForRead().CopyToAsync(fileStream);
                        await fileStream.FlushAsync();

                        return new ExportResult(ExportState.Succeeded, file);
                    }
                }
                finally
                {
                    stream?.Dispose();
                }
            }
            catch (Exception ex)
            {
                if (targetFolder is null)
                    await Ioc.Default.GetService<IDialogService>()
                        .ShowMessageAsync(ex.Message, Localization.Get("SaveImageError"));
            }

            return ExportResult.CreatedFailed();
        }

        public static async Task<IRandomAccessStream> GetGlyphPNGStreamAsync(ExportOptions style, CharacterRenderingOptions options, Character selectedChar)
        {
            // 1. First we should check if we should actually render this
            float size = style.PreferredSize > 0 ? (float)style.PreferredSize : (float)ResourceHelper.AppSettings.PngSize;
            var r = ResourceHelper.AppSettings.PngSize / 2;

            var textColor = style.PreferredColor;

            using CanvasTextLayout layout =
                CreateLayout(
                    options with { FontSize = size },
                    selectedChar,
                    style.PreferredStyle,
                    size);

            var db = layout.DrawBounds;

            if (style.SkipEmptyGlyphs && db.Height == 0 && db.Width == 0)
                return null;

            IRandomAccessStream stream = null;
            // If the glyph is actually a PNG file inside the font we should export it directly.
            // TODO : We're not actually exporting with typography options here.
            //        Find a test PNG font with typography
            if (options.Analysis.GlyphFormats.Contains(GlyphImageFormat.Png))
            {
                IBuffer buffer = GetGlyphBuffer(options.Variant.Face, selectedChar.UnicodeIndex, GlyphImageFormat.Png);
                stream = buffer.AsStream().AsRandomAccessStream();
            }
            else
            {
                var device = Utils.CanvasDevice;
                var localDpi = 96; //Windows.Graphics.Display.DisplayInformation.GetForCurrentView().LogicalDpi;

                using var renderTarget = new CanvasRenderTarget(device, size, size, localDpi);
                using (var ds = renderTarget.CreateDrawingSession())
                {
                    ds.Clear(Colors.Transparent);

                    double scale = Math.Min(1, Math.Min(size / db.Width, size / db.Height));
                    var x = -db.Left + ((size - (db.Width * scale)) / 2d);
                    var y = -db.Top + ((size - (db.Height * scale)) / 2d);

                    ds.Transform =
                        Matrix3x2.CreateTranslation(new Vector2((float)x, (float)y))
                        * Matrix3x2.CreateScale(new Vector2((float)scale));

                    ds.DrawTextLayout(layout, new (0), textColor);
                }

                stream = new InMemoryRandomAccessStream();
                await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);
            }

            stream.Seek(0);
            return stream;
        }

        private static CanvasTextLayout CreateLayout(
            CharacterRenderingOptions options,
            Character character,
            ExportStyle style,
            float canvasSize)
        {
            CanvasTextFormat format = Utils.GetInterop().CreateTextFormat(
                options.Variant.Face,
                options.Variant.DirectWriteProperties.Weight,
                options.Variant.DirectWriteProperties.Style,
                options.Variant.DirectWriteProperties.Stretch,
                options.FontSize);
            format.HorizontalAlignment = CanvasHorizontalAlignment.Center;

            CanvasTextLayout layout = new (Utils.CanvasDevice, $"{character}", format, canvasSize, canvasSize);

            if (style == ExportStyle.ColorGlyph)
                layout.Options = CanvasDrawTextOptions.EnableColorFont;

            layout.SetTypography(0, 1, options.CreateCanvasTypography());

            return layout;
        }
        private static IBuffer GetGlyphBuffer(DWriteFontFace fontface, uint unicodeIndex, GlyphImageFormat format)
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
            /* SVG Exports render at fixed size - but a) they're vectors, and b) they're
             * inside an auto-scaling viewport. So render-size is *largely* pointless */

            using var layout = CreateLayout(options, selectedChar, ExportStyle.ColorGlyph, options.FontSize);
            layout.Options = options.Analysis.GlyphFormats.Contains(GlyphImageFormat.Svg) 
                ? CanvasDrawTextOptions.EnableColorFont 
                : CanvasDrawTextOptions.Default;
            
            return CanvasGeometry.CreateText(layout);
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

        internal static async Task<ExportGlyphsResult> ExportGlyphsToFolderAsync(
            InstalledFont family, 
            CharacterRenderingOptions options, 
            IReadOnlyList<Character> characters,
            ExportOptions opts,
            Action<int, int> callback,
            CancellationToken token)
        {
            if (await PickFolderAsync() is StorageFolder folder)
            {
                List<ExportResult> fails = new();
                List<ExportResult> skips = new();
                NativeInterop interop = Utils.GetInterop();

                // TODO: Parallelise this to improve export speed
                // TODO: Requires UI thread because SVG geometry parsing
                //       uses XAML geometry. See if we can find a faster path.
                int i = 0;
                foreach (var c in characters)
                {
                    if (token.IsCancellationRequested)
                        break;

                    i++;

                    callback?.Invoke(i, characters.Count);

                    // We need to create a new analysis for each individual glyph to properly
                    // support export non-outline glyphs
                    using var layout = CreateLayout(options, c, opts.PreferredStyle, 1024f);
                    options = options with { Analysis = interop.AnalyzeCharacterLayout(layout) };

                    // Export the glyph
                    ExportResult result = await ExportGlyphAsync(opts, family, options, c, folder);
                    if (result is not null)
                    {
                        if (result.State == ExportState.Failed)
                            fails.Add(result);
                        else if (result.State == ExportState.Skipped)
                            skips.Add(result);
                    }
                }

                return new ExportGlyphsResult(
                    true, i - fails.Count - skips.Count, folder, fails.Count, skips.Count); ;
            }

            return null;
        }
    }
}
