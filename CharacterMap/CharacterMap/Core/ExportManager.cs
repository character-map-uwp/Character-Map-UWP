using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Svg;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI;

namespace CharacterMap.Core;

public enum ExportFormat : int { Png = 0, Svg = 1 }

public enum ExportStyle { Black, White, ColorGlyph }

public enum ExportState { Skipped, Succeeded, Failed }

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

public class ExportCharactersResult
{
    public StorageFolder Folder { get; }
    public int Failed { get; }
    public int Skipped { get; }
    public bool Success { get; }
    public int Count { get; }

    public ExportCharactersResult(bool success, int count, StorageFolder folder, int failed, int skipped)
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
            && !options.Analysis.GlyphFormats.Contains(GlyphImageFormat.Svg))
        {
            NativeInterop interop = Utils.GetInterop();
            Rect bounds = Rect.Empty;

            List<string> pathElements = new();
            List<string> defsElements = new();

            List<ushort[]> indiciesList = new();
            if (options.Analysis.Indicies != null && options.Analysis.Indicies.Length > 0)
            {
                foreach (var thing in options.Analysis.Indicies)
                {
                    ushort[] arr = thing.ToArray();
                    if (arr.Length > 0)
                        indiciesList.Add(arr);
                }
            }

            if (indiciesList.Count == 0)
            {
                int targetGlyphIndex = -1;
                if (selectedChar is GlyphCharacter gc)
                    targetGlyphIndex = gc.GlyphIndex;
                else if (selectedChar != null)
                {
                    int[] indices = options.Variant.FontFace.GetGlyphIndices(new[] { selectedChar.UnicodeIndex });
                    if (indices != null && indices.Length > 0 && indices[0] != 0)
                        targetGlyphIndex = indices[0];
                }

                if (targetGlyphIndex >= 0)
                    indiciesList.Add(new ushort[] { (ushort)targetGlyphIndex });
            }

            foreach (var thing in indiciesList)
            {
                var colorDatas = interop.GetColorPathDatas(options.Variant.Face, thing);
                foreach (var colorData in colorDatas)
                {
                    if (colorData == null || string.IsNullOrWhiteSpace(colorData.Path))
                        continue;

                    if (!colorData.Bounds.IsEmpty)
                    {
                        if (bounds.IsEmpty)
                        {
                            bounds = colorData.Bounds;
                        }
                        else
                        {
                            var left = Math.Min(bounds.Left, colorData.Bounds.Left);
                            var top = Math.Min(bounds.Top, colorData.Bounds.Top);
                            var right = Math.Max(bounds.Right, colorData.Bounds.Right);
                            var bottom = Math.Max(bounds.Bottom, colorData.Bounds.Bottom);
                            bounds = new Rect(left, top, right - left, bottom - top);
                        }
                    }

                    string fillAttr = "";
                    if (!string.IsNullOrEmpty(colorData.PaintReference))
                    {
                        if (colorData.IsClip)
                            fillAttr = $" clip-path=\"{colorData.PaintReference}\"";
                        else
                            fillAttr = $" fill=\"{colorData.PaintReference}\"";
                    }
                    else
                    {
                        var hex = $"#{colorData.Color.R:X2}{colorData.Color.G:X2}{colorData.Color.B:X2}";
                        fillAttr = $" fill=\"{hex}\"";
                        if (colorData.Color.A != 255)
                        {
                            double opacity = colorData.Color.A / 255.0;
                            fillAttr += $" fill-opacity=\"{opacity:F2}\"";
                        }
                    }

                    if (colorData.FillRuleEvenOdd)
                    {
                        fillAttr += " fill-rule=\"evenodd\"";
                    }

                    if (!string.IsNullOrEmpty(colorData.PaintDefinition))
                    {
                        if (!defsElements.Contains(colorData.PaintDefinition))
                            defsElements.Add(colorData.PaintDefinition);
                    }

                    pathElements.Add($"<path d=\"{colorData.Path}\"{fillAttr} />");
                }
            }

            if (pathElements.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{bounds.X} {bounds.Y} {bounds.Width} {bounds.Height}\">");
                if (defsElements.Count > 0)
                    sb.Append("<defs>").Append(string.Join("", defsElements)).Append("</defs>");
                sb.Append(string.Join("", pathElements));
                sb.Append("</svg>");
                return sb.ToString();
            }
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
            // Infer a glyph index.
            int targetGlyphIndex = -1;
            if (selectedChar is GlyphCharacter gc)
            {
                targetGlyphIndex = gc.GlyphIndex;
            }
            else if (selectedChar != null)
            {
                // SVG font glyphs are created from at most a single glyph per character.
                int[] indices = options.Variant.FontFace.GetGlyphIndices(new[] { selectedChar.UnicodeIndex });
                if (indices != null && indices.Length > 0 && indices[0] != 0)
                    targetGlyphIndex = indices[0];
                else
                    targetGlyphIndex = (int)selectedChar.UnicodeIndex;
            }

            try
            {
               
                IBuffer b = GetCharacterBuffer(options.Variant.Face, selectedChar, GlyphImageFormat.Svg);
                string str = null;
                if (targetGlyphIndex >= 0)
                    str = SVGGlyphHelper.FilterSVGToGlyph(targetGlyphIndex, b);
                else
                    str = SVGGlyphHelper.ReadSVGBuffer(b);

                // This is the most fool-proof way of calcuation bounds I've found; essentially
                // render the entire thing.
                return SVGGlyphHelper.FitBounds(str, options.Variant.Face.DesignUnitsPerEm);
            }
            catch (Exception ex)
            {
                Utils.AppendDiagnostics($"ExportManager Get SVG ({e.Font.Name})", ex);

                // Try to fallback to monochrome glyphs (though many SVG fonts won't include them)
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

        if (selectedChar is GlyphCharacter gc)
        {
            var options = e.Options with { FontSize = size };
            var device = Utils.CanvasDevice;
            var localDpi = 96;
            IRandomAccessStream stream = null;

            // Path 1: glyph is an embedded PNG bitmap inside the font
            if (e.Options.Analysis.GlyphFormats.Contains(GlyphImageFormat.Png))
            {
                IBuffer buffer = GetCharacterBuffer(e.Options.Variant.Face, gc, GlyphImageFormat.Png);
                stream = buffer.AsStream().AsRandomAccessStream();
            }
            // Path 2: glyph is stored as SVG inside the font (e.g. Noto Color Emoji)
            else if (e.Options.Analysis.GlyphFormats.Contains(GlyphImageFormat.Svg))
            {
                try
                {
                    IBuffer svgBuffer = GetCharacterBuffer(e.Options.Variant.Face, gc, GlyphImageFormat.Svg);
                    string svgStr = SVGGlyphHelper.FilterSVGToGlyph(gc.GlyphIndex, svgBuffer);

                    using CanvasSvgDocument svgDoc = CanvasSvgDocument.LoadFromXml(device, svgStr);

                    // The SVG buffer was fetched at ppem=1024. Draw to a 1024x1024 intermediate
                    // surface (matching the ppem) so the SVG's internal coordinate system aligns.
                    const float svgPpem = 1024f;

                    // Measure actual rendered bounds in the ppem coordinate space
                    Rect svgBounds;
                    using (CanvasCommandList cl = new(device))
                    {
                        using CanvasDrawingSession cds = cl.CreateDrawingSession();
                        cds.DrawSvg(svgDoc, new Size(svgPpem, svgPpem));
                        svgBounds = cl.GetBounds(device);
                    }

                    if (e.SkipEmptyGlyphs && !svgBounds.HasDimensions())
                        return null;

                    if (!svgBounds.HasDimensions())
                        svgBounds = new Rect(0, 0, svgPpem, svgPpem);

                    using var renderTarget = new CanvasRenderTarget(device, size, size, localDpi);
                    using (var ds = renderTarget.CreateDrawingSession())
                    {
                        ds.Clear(Colors.Transparent);
                        double scale = Math.Min(size / svgBounds.Width, size / svgBounds.Height);
                        float x = (float)((size - svgBounds.Width * scale) / 2d - svgBounds.Left * scale);
                        float y = (float)((size - svgBounds.Height * scale) / 2d - svgBounds.Top * scale);
                        ds.Transform =
                            Matrix3x2.CreateScale((float)scale)
                            * Matrix3x2.CreateTranslation(x, y);
                        ds.DrawSvg(svgDoc, new Size(svgPpem, svgPpem));
                    }

                    stream = new InMemoryRandomAccessStream();
                    await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);
                }
                catch
                {
                    // Fall through to geometry path below if SVG rendering fails
                }
            }
            // Path 3: glyph uses COLR colour layers
            else if ((e.Options.Analysis.GlyphFormats.Contains(GlyphImageFormat.Colr)
                      || e.Options.Analysis.GlyphFormats.Contains(GlyphImageFormat.ColrPaintTree))
                     && e.PreferredStyle == ExportStyle.ColorGlyph)
            {
                // Render via a glyph run — measure bounds using a CanvasCommandList first
                CanvasGlyph[] glyphs = [new() { Index = gc.GlyphIndex }];
                using CanvasCommandList cl = new(device);
                using (CanvasDrawingSession cds = cl.CreateDrawingSession())
                    cds.DrawGlyphRun(Vector2.Zero, e.Options.Variant.FontFace, size, glyphs, false, 0,
                        new CanvasSolidColorBrush(device, textColor));
                Rect colrBounds = cl.GetBounds(device);

                if (e.SkipEmptyGlyphs && !colrBounds.HasDimensions())
                    return null;

                if (colrBounds.HasDimensions() is false)
                    colrBounds = new Rect(0, 0, size, size);

                using var rt = new CanvasRenderTarget(device, size, size, localDpi);
                using (var ds = rt.CreateDrawingSession())
                {
                    ds.Clear(Colors.Transparent);
                    double scale = Math.Min(size / colrBounds.Width, size / colrBounds.Height);
                    float x = (float)((size - colrBounds.Width * scale) / 2d - colrBounds.Left * scale);
                    float y = (float)((size - colrBounds.Height * scale) / 2d - colrBounds.Top * scale);
                    ds.Transform =
                        Matrix3x2.CreateScale((float)scale)
                        * Matrix3x2.CreateTranslation(x, y);
                    ds.DrawGlyphRun(Vector2.Zero, e.Options.Variant.FontFace, size, glyphs, false, 0,
                        new CanvasSolidColorBrush(device, textColor));
                }
                stream = new InMemoryRandomAccessStream();
                await rt.SaveAsync(stream, CanvasBitmapFileFormat.Png);
            }

            // Path 4: monochrome geometry (TTF/CFF outlines)
            if (stream is null)
            {
                using CanvasGeometry geom = CreateGeometry(gc, options);
                var db = geom.ComputeBounds();

                if (e.SkipEmptyGlyphs && !db.HasDimensions())
                    return null;

                using var renderTarget = new CanvasRenderTarget(device, size, size, localDpi);
                using (var ds = renderTarget.CreateDrawingSession())
                {
                    ds.Clear(Colors.Transparent);
                    double scale = Math.Min(1, Math.Min(size / db.Width, size / db.Height));
                    float x = (float)((size - db.Width * scale) / 2d - db.Left * scale);
                    float y = (float)((size - db.Height * scale) / 2d - db.Top * scale);
                    ds.Transform =
                        Matrix3x2.CreateScale((float)scale)
                        * Matrix3x2.CreateTranslation(x, y);
                    ds.FillGeometry(geom, textColor);
                }
                stream = new InMemoryRandomAccessStream();
                await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);
            }

            stream.Seek(0);
            return stream;
        }

        using CanvasTextLayout layout =
            CreateLayout(
                e.Options with { FontSize = size },
                selectedChar,
                e.PreferredStyle,
                size);

        // For color/SVG glyphs, layout.DrawBounds only reflects the monochrome outline bounds —
        // the actual rendered SVG/COLR artwork can extend beyond this. Measure actual rendered
        // bounds via a CanvasCommandList pre-render pass to avoid cut-off exports.
        Rect db_layout;
        bool isColorGlyph = e.PreferredStyle == ExportStyle.ColorGlyph
            && e.Options.Analysis.HasColorGlyphs;
        if (isColorGlyph)
        {
            using CanvasCommandList cl = new(Utils.CanvasDevice);
            using (CanvasDrawingSession ds = cl.CreateDrawingSession())
                ds.DrawTextLayout(layout, new(0), textColor);

            Rect measuredBounds = cl.GetBounds(Utils.CanvasDevice);
            db_layout = measuredBounds.HasDimensions()
                ? measuredBounds
                : layout.DrawBounds;
        }
        else
        {
            db_layout = layout.DrawBounds;
        }

        if (e.SkipEmptyGlyphs && db_layout.Height == 0 && db_layout.Width == 0)
            return null;

        IRandomAccessStream stream_layout = null;
        // If the glyph is actually a PNG file inside the font we should export it directly.
        // TODO : We're not actually exporting with typography options here.
        //        Find a test PNG font with typography
        if (e.Options.Analysis.GlyphFormats.Contains(GlyphImageFormat.Png))
        {
            IBuffer buffer = GetCharacterBuffer(e.Options.Variant.Face, selectedChar, GlyphImageFormat.Png);
            stream_layout = buffer.AsStream().AsRandomAccessStream();
        }
        else
        {
            var device = Utils.CanvasDevice;
            var localDpi = 96; //Windows.Graphics.Display.DisplayInformation.GetForCurrentView().LogicalDpi;

            using var renderTarget = new CanvasRenderTarget(device, size, size, localDpi);
            using (var ds = renderTarget.CreateDrawingSession())
            {
                ds.Clear(Colors.Transparent);

                double scale = Math.Min(1, Math.Min(size / db_layout.Width, size / db_layout.Height));
                float x = (float)((size - db_layout.Width * scale) / 2d - db_layout.Left * scale);
                float y = (float)((size - db_layout.Height * scale) / 2d - db_layout.Top * scale);

                ds.Transform =
                    Matrix3x2.CreateScale((float)scale)
                    * Matrix3x2.CreateTranslation(x, y);

                ds.DrawTextLayout(layout, new(0), textColor);
            }

            stream_layout = new InMemoryRandomAccessStream();
            await renderTarget.SaveAsync(stream_layout, CanvasBitmapFileFormat.Png);
        }

        stream_layout.Seek(0);
        return stream_layout;
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
    private static IBuffer GetCharacterBuffer(DWriteFontFace fontface, Character c, GlyphImageFormat format)
    {
        if (c is GlyphCharacter gc)
            return DirectWrite.GetGlyphImageDataBuffer(fontface, 1024, gc.GlyphIndex, format);

        return DirectWrite.GetImageDataBuffer(fontface, 1024, c.UnicodeIndex, format);
    }

    internal static string GetFileName(
        ExportOptions e,
        Character c,
        string ext) 
        => e.GetFileName(c, ext);

    private static Task<StorageFile> PickFileAsync(string fileName, string key, IList<string> values, PickerLocationId suggestedLocation = PickerLocationId.PicturesLibrary)
        => StorageHelper.PickSaveFileAsync(fileName, key, values, suggestedLocation);

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
        if (!bounds.HasDimensions())
        {
            bounds = new Rect(0, 0, 512, 512);
        }

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

        if (selectedChar is GlyphCharacter gc)
        {
            CanvasGlyph[] glyphs = [ new() { Index = gc.GlyphIndex } ];
            return CanvasGeometry.CreateGlyphRun(
                Utils.CanvasDevice,
                Vector2.Zero,
                options.Variant.FontFace,
                512, //options.FontSize,
                glyphs,
                isSideways: false,
                bidiLevel: 0,
                CanvasTextMeasuringMode.Natural,
                CanvasGlyphOrientation.Upright);
        }

        using var layout = CreateLayout(options, selectedChar, ExportStyle.ColorGlyph, options.FontSize);
        layout.Options = options.Analysis.GlyphFormats.Contains(GlyphImageFormat.Svg)
            ? CanvasDrawTextOptions.EnableColorFont
            : CanvasDrawTextOptions.Default;

        return CanvasGeometry.CreateText(layout);
    }

    private static IAsyncOperation<StorageFolder> PickFolderAsync() => StorageHelper.PickFolderAsync();

    internal static async Task<ExportCharactersResult> ExportCharactersToFolderAsync(
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

            return new ExportCharactersResult(
                true, i - fails.Count - skips.Count, folder, fails.Count, skips.Count); ;
        }

        return null;
    }
}
