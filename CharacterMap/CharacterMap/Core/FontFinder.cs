using System.Diagnostics;
using Windows.Storage.Search;
using Windows.UI.Text;
using WoffToOtf;

namespace CharacterMap.Core;

public class FontFinder
{
    private static SemaphoreSlim _initSemaphore { get; } = new(1, 1);
    private static SemaphoreSlim _loadSemaphore { get; } = new(1, 1);


    public static Dictionary<string, CMFontFamily> FontDictionary { get; private set; }
    public static IReadOnlyList<CMFontFamily> Fonts { get; private set; }
    public static IReadOnlyList<CMFontFamily> ImportedFonts { get; private set; }

    public static CMFontFamily DefaultFont { get; private set; }
    public static bool HasAppxFonts { get; private set; }
    public static bool HasRemoteFonts { get; private set; }
    public static bool HasVariableFonts { get; private set; }

    public static int SystemFamilyCount { get; private set; }
    public static int SystemFaceCount { get; private set; }

    public static int ImportedFamilyCount { get; private set; }
    public static int ImportedFaceCount { get; private set; }

    public static DWriteFallbackFont Fallback { get; private set; }

  



    public static async Task<DWriteFontSet> InitialiseAsync()
    {
        await _initSemaphore.WaitAsync().ConfigureAwait(false);

        NativeInterop interop = Utils.GetInterop();
        DWriteFontSet systemFonts = interop.GetSystemFonts();

        Parallel.ForEach(systemFonts.Families, new ParallelOptions { MaxDegreeOfParallelism = 50 }, l =>
        {
            l.Inflate();
        });
        systemFonts.Update();

        try
        {
            if (DefaultFont == null)
            {
                DWriteFontFace segoe = systemFonts.Fonts.FirstOrDefault(
                       f => f.Properties.FamilyName == "Segoe UI"
                            && f.Properties.Weight.Weight == FontWeights.Normal.Weight
                            && f.Properties.Stretch == FontStretch.Normal
                            && f.Properties.Style == FontStyle.Normal);

                if (segoe != null)
                    DefaultFont = CMFontFamily.CreateDefault(segoe);
            }
        }
        finally
        {
            _initSemaphore.Release();
        }

        return systemFonts;
    }



    public static Task LoadFontsAsync(bool clearExisting = true)
    {
        // It's possible to go down this path if the font collection
        // was loaded during app pre-launch
        if (clearExisting is false && Fonts is not null)
            return Task.CompletedTask;

        Fonts = null;

        return Task.Run(async () =>
        {
            // Reset meta to false;
            UpdateMeta(null);

            await _loadSemaphore.WaitAsync().ConfigureAwait(false);
            NativeInterop interop = Utils.GetInterop();

#if DEBUG
            Stopwatch s = new Stopwatch();
            s.Start();
#endif

            // Load SystemFonts and imported fonts in parallel
            // 1.1. Load imported fonts
            IReadOnlyList<StorageFile> files = null;
            Task<List<DWriteFontSet>> setsTask = Task.Run(async () =>
            {
                files = await FontImporter.ImportFolder.GetFilesAsync();
                var sets = interop.GetFonts(files).ToList();
                return sets;
            });

            // 1.2. Perform cleanup
            Task delete = DefaultFont == null ? Task.WhenAll(
                    FontImporter.CleanUpTempFolderAsync(),
                    FontImporter.CleanUpPendingDeletesAsync()) : Task.CompletedTask;

            // 1.3. Load installed fonts
            Task<DWriteFontSet> init = InitialiseAsync();

            await Task.WhenAll(init, delete, setsTask);

            // Load in System Fonts
            DWriteFontSet systemFonts = init.Result;
            Dictionary<string, CMFontFamily> resultList = new(systemFonts.Fonts.Count);
            UpdateMeta(systemFonts);

            /* Add imported fonts */
            IReadOnlyList<DWriteFontSet> sets = setsTask.Result;
            ImportedFamilyCount = ImportedFaceCount = 0;
            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                if (FontImporter.IgnoredFonts.Contains(file.Name))
                    continue;

                DWriteFontSet importedFonts = sets[i];
                UpdateMeta(importedFonts);
                ImportedFaceCount += importedFonts.FaceCount;
                ImportedFamilyCount += importedFonts.Families.Count;

                foreach (DWriteFontFace font in importedFonts.Fonts)
                {
                    AddFont(resultList, font, file);
                }
            }

            var imports = resultList.ToDictionary(d => d.Key, v => v.Value.Clone());

            /* Add all system fonts */
            SystemFamilyCount = systemFonts.Families.Count;
            SystemFaceCount = systemFonts.FaceCount;

            foreach (var font in systemFonts.Fonts)
                AddFont(resultList, font);

            /* Order everything appropriately */
            Fonts = CreateFontList(resultList);
            ImportedFonts = CreateFontList(imports);
            FontDictionary = resultList;

            if (Fallback == null)
                Fallback = interop.CreateEmptyFallback();

#if DEBUG
            s.Stop();
            var elasped = s.Elapsed;
#endif

            _loadSemaphore.Release();

            WeakReferenceMessenger.Default.Send(new FontListCreatedMessage());
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<CMFontFamily> CreateFontList(Dictionary<string, CMFontFamily> fonts)
    {
        return fonts.OrderBy(f => f.Key).Select(f =>
        {
            f.Value.SortVariants();
            return f.Value;
        }).ToList();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateMeta(DWriteFontSet set)
    {
        if (set == null)
        {
            HasRemoteFonts = HasAppxFonts = HasVariableFonts = false;
            return;
        }

        HasRemoteFonts = HasRemoteFonts || set.CloudFontCount > 0;
        HasAppxFonts = HasAppxFonts || set.AppxFontCount > 0;
        HasVariableFonts = true;
    }

    internal static List<CMFontFace> GetImportedVariants(bool includeSimulations = false)
    {
        return Fonts.Where(f => f.HasImportedFiles)
                    .SelectMany(f => f.Variants.Where(v => v.IsImported 
                        && (includeSimulations ? true : v.DirectWriteProperties.IsSimulated is false)))
                    .ToList();
    }

    internal static List<CMFontFace> GetSystemVariants(bool includeSimulations = false)
    {
        return Fonts.SelectMany(f => f.Variants.Where(v => v.IsImported is false
                        && (includeSimulations ? true : v.DirectWriteProperties.IsSimulated is false)))
                    .ToList();
    }

    /* 
     * Helper method for adding fonts. 
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddFont(
        IDictionary<string, CMFontFamily> fontList,
        DWriteFontFace font,
        StorageFile file = null)
    {
        try
        {
            if (font.Properties.IsSimulated && ResourceHelper.AppSettings.HideSimulatedFontFaces)
                return;

            var familyName = font.Properties.FamilyName;
            if (!string.IsNullOrEmpty(familyName))
            {
                /* Check if we already have a listing for this fontFamily */
                if (fontList.TryGetValue(familyName, out var fontFamily))
                {
                    fontFamily.AddVariant(font, file);
                }
                else
                {
                    fontList[familyName] = new CMFontFamily(familyName, font, file);
                }
            }
        }
        catch (Exception)
        {
            // Corrupted font files throw an exception
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetAppPath(StorageFile file)
    {
        return GetAppPath(file.Path);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetAppPath(string path)
    {
        if (path.StartsWith(ApplicationData.Current.TemporaryFolder.Path, StringComparison.InvariantCultureIgnoreCase))
        {
            var str = path.Replace(ApplicationData.Current.TemporaryFolder.Path, "ms-appdata:///temp", StringComparison.InvariantCultureIgnoreCase)
                .Replace("\\", "/");
            return str;
        }
        var temp = Path.GetDirectoryName(path).EndsWith(FontImporter.TEMP);
        return $"ms-appdata:///local/{(temp ? $"{FontImporter.TEMP}/" : string.Empty)}{Path.GetFileName(path)}";
    }

    /// <summary>
    /// Returns true if all fonts were deleted.
    /// Returns false is some failed - these will be deleted on next start up.
    /// </summary>
    /// <param name="font"></param>
    /// <returns></returns>
    internal static Task<bool> RemoveFontAsync(CMFontFamily font)
    {
        Fonts = null;
        return FontImporter.TryRemoveFontAsync(font);
    }

    public static bool IsMDL2(CMFontFace variant) => variant != null && (
        variant.FamilyName.Contains("MDL2") || variant.FamilyName.Contains("Fluent Icons"));

    public static bool IsSystemSymbolFamily(CMFontFace variant) => variant != null && (
        variant.FamilyName.Equals("Segoe MDL2 Assets") || variant.FamilyName.Equals("Segoe Fluent Icons"));


    public static FontQueryResults QueryFontList(
        string query, 
        IEnumerable<CMFontFamily> fontList, 
        UserCollectionsService fontCollections,
        IFontCollection collection = null,
        BasicFontFilter filter = null)
    {
        filter ??= BasicFontFilter.All;
        string filterTitle = null;

        if (!string.IsNullOrWhiteSpace(query))
        {
            string q;
            if (IsQuery(query, Localization.Get("CharacterFilter"), "char:", out q))
            {
                foreach (var ch in q)
                {
                    if (ch == ' ')
                        continue;
                    fontList = BasicFontFilter.ForChar(new(ch)).Query(fontList, fontCollections);
                }
                filterTitle = $"{filter.FilterTitle} \"{q}\"";
            }
            else if (IsQuery(query, Localization.Get("FilePathFilter"), "filepath:", out q))
            {
                fontList = BasicFontFilter.ForFilePath(q).Query(fontList, fontCollections);
                filterTitle = $"{filter.FilterTitle} \"{q}\"";

            }
            else if (IsQuery(query, Localization.Get("FoundryFilter"), "foundry:", out q))
            {
                fontList = BasicFontFilter.ForFontInfo(q, Microsoft.Graphics.Canvas.Text.CanvasFontInformation.Manufacturer).Query(fontList, fontCollections);
                filterTitle = $"{filter.FilterTitle} \"{q}\"";
            }
            else if (IsQuery(query, Localization.Get("DesignerFilter"), "designer:", out q))
            {
                fontList = BasicFontFilter.ForFontInfo(q, Microsoft.Graphics.Canvas.Text.CanvasFontInformation.Designer).Query(fontList, fontCollections);
                filterTitle = $"{filter.FilterTitle} \"{q}\"";
            }
            else
            {
                fontList = fontList.Where(f => f.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
                string prefix = filter == BasicFontFilter.All ? "" : filter.FilterTitle + " ";
                filterTitle = $"{(collection != null ? collection.Name + " " : prefix)}\"{query}\"";
            }

            return new(fontList, filterTitle, true);
        }
        
        return new(fontList, null, false);
    }

    public static bool IsQuery(string q, string i, string i2, out string o)
    {
        string s = i;
        if (q.StartsWith(s, StringComparison.OrdinalIgnoreCase) is false)
            s = i2;

        if (q.StartsWith(s, StringComparison.OrdinalIgnoreCase)
            && q.Remove(0, s.Length).Trim() is string t
            && !string.IsNullOrWhiteSpace(t))
        {
            o = t;
            return true;
        }

        o = null;
        return false;
    }
}