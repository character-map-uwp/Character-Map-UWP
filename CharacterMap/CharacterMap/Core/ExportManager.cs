using CharacterMapCX;
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





    //------------------------------------------------------
    //
    //  SVG
    //
    //------------------------------------------------------

    public static string GetSVG(
        ExportOptions e,
        Character selectedChar,
        bool skipEmpty = false)
    {
        // We want to prepare geometry at 1024px
        var options = e.Options with { FontSize = 1024 };

        // If COLR format (e.g. Segoe UI Emoji), we have special export path.
        // This path does not require UI thread.
        if (e.PreferredStyle == ExportStyle.ColorGlyph
            && options.Analysis.HasColorGlyphs
            && !options.Analysis.GlyphFormats.Has(GlyphImageFormat.Svg))
        {
            // COLRv1: use the native paint-reader → SVG path (richer: gradients, composites, etc.)
            if (options.Analysis.SupportsColrV1 && (e.PreferredColorType is GlyphImageFormat.None or GlyphImageFormat.ColrPaintTree))
            {
                uint glyphIdx = selectedChar is GlyphCharacter gc2
                    ? gc2.GlyphIndex
                    : options.Face.GetGlyphIndex(selectedChar);

                if (glyphIdx > 0)
                {
                    try
                    {
                        string colrV1Svg = DirectWrite.GetColrV1Svg(
                            options.ActiveFontFace,
                            (ushort)glyphIdx,
                            e.PreferredColor);
                        if (!string.IsNullOrWhiteSpace(colrV1Svg))
                            return colrV1Svg;
                    }
                    catch (Exception ex)
                    {
                        Utils.AppendDiagnostics($"ExportManager GetColrV1Svg ({e.Font.Name})", ex);
                    }
                }
            }

            // COLRv0: multi-layer coloured paths from the existing analysis
            NativeInterop interop = Utils.GetInterop();
            List<string> paths = new();
            Rect bounds = Rect.Empty;

            // Try to find the bounding box of all glyph layers combined
            foreach (var thing in options.Analysis.Indicies)
            {
                var path = interop.GetPathDatas(options.ActiveFontFace, thing.ToArray()).First();
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

            return Utils.GenerateSvgString(bounds, paths, options.Analysis.Colors);
        }

        var data = GetGeometry(selectedChar, options);

        if (string.IsNullOrWhiteSpace(data.Path) && skipEmpty)
            return null;

        string GetMonochrome()
        {
            return string.IsNullOrWhiteSpace(data.Path)
                ? string.Empty
                : Utils.GenerateSvgString(data.Bounds, data.Path, e.PreferredColor);
        }

        // If the font uses SVG glyphs, we can extract the raw SVG from the font file.
        // This path requires access to the UI thread.
        if (options.Analysis.GlyphFormats.Has(GlyphImageFormat.Svg))
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
                uint indice = options.Face.GetGlyphIndex(selectedChar);
                if (indice != 0)
                    targetGlyphIndex = (int)indice;
                else
                    targetGlyphIndex = (int)selectedChar.UnicodeIndex;
            }

            try
            {
                IBuffer b = GetCharacterBuffer(options.ActiveFontFace, selectedChar, GlyphImageFormat.Svg);
                string str = null;
                if (targetGlyphIndex >= 0)
                    str = SVGGlyphHelper.FilterSVGToGlyph(targetGlyphIndex, b);
                else
                    str = SVGGlyphHelper.ReadSVGBuffer(b);

                return SVGGlyphHelper.FitBounds(str, options.ActiveFontFace.DesignUnitsPerEm);
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

    public static async Task<ExportResult> ExportSvgAsync(
        ExportOptions e,
        Character selectedChar)
    {
        try 
        {
            // 1. Check if we should actually save the file.
            //    Certain export modes will skip blank geometries
            string svg = GetSVG(e, selectedChar, e.SkipEmptyGlyphs);
            if (string.IsNullOrWhiteSpace(svg) && e.SkipEmptyGlyphs)
                return new ExportResult(ExportState.Skipped, null);

            // 2. Get the file we will save the image to.
            var providedFile = await GetTargetFileAsync(e, selectedChar, "svg", e.TargetFolder);
            if (providedFile is StorageFile file)
            {
                // 3. Write the SVG to the file
                await Utils.WriteSvgAsync(svg, file);
                return new ExportResult(ExportState.Succeeded, file);
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

    private static IBuffer GetCharacterBuffer(DWriteFontFace fontface, Character c, GlyphImageFormat format)
    {
        if (c is GlyphCharacter gc)
            return DirectWrite.GetGlyphImageDataBuffer(fontface, 1024, gc.GlyphIndex, format);

        return DirectWrite.GetImageDataBuffer(fontface, 1024, c.UnicodeIndex, format);
    }





    //------------------------------------------------------
    //
    //  PNG
    //
    //------------------------------------------------------

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
        float size = e.PreferredSize > 0 ? (float)e.PreferredSize : (float)ResourceHelper.AppSettings.PngSize;
        Color textColor = e.PreferredColor;
        bool isColor = e.PreferredStyle == ExportStyle.ColorGlyph;

        if (selectedChar is GlyphCharacter gc)
            return DirectWrite.GetGlyphPNGStream(e.Options.ActiveFontFace, (ushort)gc.GlyphIndex, size, textColor, e.PreferredColorType);

        IReadOnlyList<uint> typographyTags = e.Options.Typography?.Select(t => (uint)t.Feature).ToList() ?? [];
        return DirectWrite.GetCharacterPNGStream(e.Options.ActiveFontFace, selectedChar.Char, size, textColor, e.PreferredColorType, typographyTags);
    }






    //------------------------------------------------------
    //
    //  Helpers
    //
    //------------------------------------------------------

    internal static string GetFileName(
        ExportOptions e,
        Character c,
        string ext) 
        => e.GetFileName(c, ext);

    public static Task<StorageFile> GetTargetFileAsync(ExportOptions e, Character c, string format, StorageFolder targetFolder)
    {
        string name = GetFileName(e, c, format);
        if (targetFolder != null)
            return targetFolder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting).AsTask();
        else
            return PickFileAsync(name, format.ToUpper(), new[] { $".{format}" });
    }

    private static Task<StorageFile> PickFileAsync(string fileName, string key, IList<string> values, PickerLocationId suggestedLocation = PickerLocationId.PicturesLibrary)
        => StorageHelper.PickSaveFileAsync(fileName, key, values, suggestedLocation);

    public static (string Path, Rect Bounds) GetGeometry(
        Character selectedChar,
        CharacterRenderingOptions options)
    {
        NativeInterop interop = Utils.GetInterop();
        float fontSize = options.FontSize > 0 ? options.FontSize : 512f;
        PathData data;
        if (selectedChar is GlyphCharacter gc)
            data = interop.GetGlyphPath(options.ActiveFontFace, (ushort)gc.GlyphIndex, fontSize);
        else
            data = interop.GetTextPath(options.ActiveFontFace, selectedChar.Char, fontSize, (options.Typography.FirstOrDefault() ?? TypographyFeatureInfo.None).Feature);

        Rect bounds = data?.Bounds ?? new Rect(0, 0, fontSize, fontSize);
        if (!bounds.HasDimensions())
            bounds = new Rect(0, 0, fontSize, fontSize);

        return (data?.Path ?? string.Empty, bounds);
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

            int i = 0;
            foreach (Character c in characters)
            {
                if (token.IsCancellationRequested)
                    break;

                i++;
                callback?.Invoke(i, characters.Count);

                CanvasTextLayoutAnalysis analysis = c is GlyphCharacter gc
                    ? interop.AnalyzeGlyphLayout(e.Options.ActiveFontFace, (ushort)gc.GlyphIndex)
                    : interop.AnalyzeCharacter(e.Options.ActiveFontFace, c.Char, (e.Options.Typography.FirstOrDefault() ?? TypographyFeatureInfo.None).Feature);

                e = e with { 
                    Options = e.Options with { Analysis = analysis } 
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
                true, i - fails.Count - skips.Count, folder, fails.Count, skips.Count);
        }

        return null;
    }
}
