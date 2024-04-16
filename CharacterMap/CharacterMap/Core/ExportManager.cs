using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Svg;
using Microsoft.Graphics.Canvas.Text;
using System.IO.Compression;
using Windows.UI;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Core;

public enum ExportFormat : int { Png = 0, Svg = 1 }

public enum ExportStyle { Black, White, ColorGlyph }

public enum ExportState { Skipped, Succeeded, Failed }

public class FileNameWriterArgs
{
    public ExportOptions Options { get; init; }
    public Character Character { get; init; }
    public string Extension { get; init; }
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
        ExportOptions e,
        Character selectedChar,
        bool skipEmpty = false)
    {
        // We want to prepare geometry at 1024px
        var options = e.Options with { FontSize = 1024 };
        using var typography = options.CreateCanvasTypography();

        CanvasDevice device = Utils.CanvasDevice;

        // If COLR format (e.g. Segoe UI Emoji), we have special export path.
        // This path does not require UI thread.
        if (e.PreferredStyle == ExportStyle.ColorGlyph
            && options.Analysis.HasColorGlyphs
            && !options.Analysis.GlyphFormats.Contains(GlyphImageFormat.Svg))
        {
            NativeInterop interop = Utils.GetInterop();
            List<string> paths = new();
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
                : Utils.GenerateSvgDocument(device, data.Bounds, data.Path, e.PreferredColor);
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
        ExportOptions e,
        Character selectedChar)
    {
        // To export a glyph as an SVG, it must be fully vector based.
        // If it is not, we force export as PNG regardless of choice.
        if (e.PreferredFormat == ExportFormat.Png || e.Options.Analysis.IsFullVectorBased is false)
            return ExportPngAsync(e, selectedChar);
        else
            // NOTE: SVG Export may require UI thread
            return ExportSvgAsync(e, selectedChar);
    }

    public static Task<StorageFile> GetTargetFileAsync(ExportOptions e, Character c, string format, StorageFolder targetFolder)
    {
        string name = GetFileName(e, c, format);
        if (targetFolder != null)
            return targetFolder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting).AsTask();
        else
            return PickFileAsync(name, format.ToUpper(), new[] { $".{format}" });
    }

    public static async Task<ExportResult> ExportSvgAsync(
        ExportOptions e,
        Character selectedChar)
    {
        try
        {
            // 0. We want to prepare geometry at 1024px, so force this
            var options = e.Options with { FontSize = 1024 };
            using var typography = options.CreateCanvasTypography();

            // 1. Check if we should actually save the file.
            //    Certain export modes will skip blank geometries
            string svg = GetSVG(e, selectedChar, e.SkipEmptyGlyphs);
            if (string.IsNullOrWhiteSpace(svg) && e.SkipEmptyGlyphs)
                return new ExportResult(ExportState.Skipped, null);

            // 2. Get the file we will save the image to.
            var providedFile = await GetTargetFileAsync(e, selectedChar, "svg", e.TargetFolder);
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
            if (e.TargetFolder is null)
                await Ioc.Default.GetService<IDialogService>()
                    .ShowMessageAsync(ex.Message, Localization.Get("SaveImageError"));
        }

        return new ExportResult(ExportState.Failed, null);
    }

    public static async Task<ExportResult> ExportPngAsync(
        ExportOptions e,
        Character selectedChar)
    {
        try
        {
            IRandomAccessStream stream = null;
            try
            {
                // 1. Try to get the glyph data
                stream = await GetGlyphPNGStreamAsync(e, selectedChar);
                if (stream is null)
                    return new ExportResult(ExportState.Skipped, null);

                // 2. Get the file we will save the image to.
                if (await GetTargetFileAsync(e, selectedChar, "png", e.TargetFolder)
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
            if (e.TargetFolder is null)
                await Ioc.Default.GetService<IDialogService>()
                    .ShowMessageAsync(ex.Message, Localization.Get("SaveImageError"));
        }

        return ExportResult.CreatedFailed();
    }

    public static async Task<IRandomAccessStream> GetGlyphPNGStreamAsync(ExportOptions e, Character selectedChar)
    {
        // 1. First we should check if we should actually render this
        float size = e.PreferredSize > 0 ? (float)e.PreferredSize : (float)ResourceHelper.AppSettings.PngSize;
        var r = ResourceHelper.AppSettings.PngSize / 2;

        var textColor = e.PreferredColor;

        using CanvasTextLayout layout =
            CreateLayout(
                e.Options with { FontSize = size },
                selectedChar,
                e.PreferredStyle,
                size);

        var db = layout.DrawBounds;

        if (e.SkipEmptyGlyphs && db.Height == 0 && db.Width == 0)
            return null;

        IRandomAccessStream stream = null;
        // If the glyph is actually a PNG file inside the font we should export it directly.
        // TODO : We're not actually exporting with typography options here.
        //        Find a test PNG font with typography
        if (e.Options.Analysis.GlyphFormats.Contains(GlyphImageFormat.Png))
        {
            IBuffer buffer = GetGlyphBuffer(e.Options.Variant.Face, selectedChar.UnicodeIndex, GlyphImageFormat.Png);
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

                ds.DrawTextLayout(layout, new(0), textColor);
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

        CanvasTextLayout layout = new(Utils.CanvasDevice, $"{character}", format, canvasSize, canvasSize);

        if (style == ExportStyle.ColorGlyph)
            layout.Options = CanvasDrawTextOptions.EnableColorFont;

        layout.SetTypography(0, 1, options.CreateCanvasTypography());
        return layout;
    }
    private static IBuffer GetGlyphBuffer(DWriteFontFace fontface, uint unicodeIndex, GlyphImageFormat format)
    {
        return DirectWrite.GetImageDataBuffer(fontface, 1024, unicodeIndex, format);
    }

    internal static string GetFileName(
        ExportOptions e,
        Character c,
        string ext) 
        => e.GetFileName(c, ext);

    private static async Task<StorageFile> PickFileAsync(string fileName, string key, IList<string> values, PickerLocationId suggestedLocation = PickerLocationId.PicturesLibrary)
    {
        FileSavePicker savePicker = new()
        {
            SuggestedStartLocation = suggestedLocation,
            SuggestedFileName = fileName
        };

        savePicker.FileTypeChoices.Add(key, values);

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
        var data = Utils.GetInterop().GetPathData(geom);

        if (string.IsNullOrWhiteSpace(data.Path))
            return (data.Path, bounds);

        var t = data.Transform.Translation;
        bounds = new Rect(t.X - bounds.Left, -bounds.Top + t.Y, bounds.Width, bounds.Height);
        return (data.Path, bounds);
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
        FolderPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add("*");

        return picker.PickSingleFolderAsync();
    }

    internal static async Task<ExportGlyphsResult> ExportGlyphsToFolderAsync(
        IReadOnlyList<Character> characters,
        ExportOptions e,
        Action<int, int> callback,
        CancellationToken token)
    {
        if (await PickFolderAsync() is StorageFolder folder)
        {
            e = e with { TargetFolder = folder };
            List<ExportResult> fails = new();
            List<ExportResult> skips = new();
            NativeInterop interop = Utils.GetInterop();

            // TODO: Parallelise this to improve export speed
            // TODO: Requires UI thread because SVG geometry parsing
            //       uses XAML geometry. See if we can find a faster path.
            int i = 0;
            foreach (Character c in characters)
            {
                if (token.IsCancellationRequested)
                    break;

                i++;
                callback?.Invoke(i, characters.Count);

                // We need to create a new analysis for each individual glyph to properly
                // support export non-outline glyphs
                using var layout = CreateLayout(e.Options, c, e.PreferredStyle, 1024f);
                e = e with { 
                    Options = e.Options with { Analysis = interop.AnalyzeCharacterLayout(layout) } 
                };

                // Export the glyph
                ExportResult result = await ExportGlyphAsync(e, c);
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
