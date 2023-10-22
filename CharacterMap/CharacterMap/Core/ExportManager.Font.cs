using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMapCX;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.Storage;
using System.IO.Compression;
using System.IO;
using Windows.Storage.Pickers;

namespace CharacterMap.Core
{
    /* This part contains code for exporting Font files from the app */

    public static partial class ExportManager
    {
        public static async void RequestExportFontFile(FontVariant variant)
        {
            var scheme = ResourceHelper.AppSettings.ExportNamingScheme;

            if (DirectWrite.IsFontLocal(variant.Face))
            {
                string filePath = GetFileName(variant, scheme);
                string name = Path.GetFileNameWithoutExtension(filePath);
                string ext = Path.GetExtension(filePath);

                if (await PickFileAsync(name, Localization.Get("ExportFontFile/Text"), new[] { ext }, PickerLocationId.DocumentsLibrary) is StorageFile file)
                {
                    try
                    {
                        bool success = await TryWriteToFileAsync(variant, file);
                        WeakReferenceMessenger.Default.Send(new AppNotificationMessage(true, new ExportFontFileResult(success, file)));
                        return;
                    }
                    catch
                    {
                    }
                }
            }

            WeakReferenceMessenger.Default.Send(new AppNotificationMessage(true, new ExportFontFileResult(null, false)));
        }

        static List<IGrouping<string, FontVariant>> GetGrouped(IList<FontVariant> fonts)
        {
            return fonts.Where(f => DirectWrite.IsFontLocal(f.Face)).GroupBy(f => DirectWrite.GetFileName(f.Face)).ToList();
        }

        internal static Task ExportCollectionAsZipAsync(
            IList<InstalledFont> fontList, 
            UserFontCollection selectedCollection,
            Action<string> callback = null)
        {
            var fonts = fontList.SelectMany(f => f.Variants).ToList();
            return ExportFontsAsZipAsync(fonts, selectedCollection.Name, callback);
        }

        internal static async Task ExportFontsAsZipAsync(
            List<FontVariant> fonts, 
            string name,
            Action<string> callback = null)
        {
            if (await PickFileAsync(name, "ZIP", new[] { ".zip" }) is StorageFile file)
            {
                callback?.Invoke($"0%");
                await Task.Run(async () =>
                {
                    ExportNamingScheme scheme = ResourceHelper.AppSettings.ExportNamingScheme;
                    var grouped = GetGrouped(fonts);

                    using var i = await file.OpenStreamForWriteAsync();
                    i.SetLength(0);

                    int c = 0;
                    using ZipArchive z = new(i, ZipArchiveMode.Create);
                    foreach (var group in grouped)
                    {

                        string fileName = GetFileName(group, scheme);
                        ZipArchiveEntry entry = z.CreateEntry(fileName);
                        using IOutputStream s = entry.Open().AsOutputStream();
                        await DirectWrite.WriteToStreamAsync(group.First().Face, s);

                        c++;
                        callback?.Invoke($"{((double)c / (double)grouped.Count) * 100:0}%");
                    }

                    await i.FlushAsync();
                });

                WeakReferenceMessenger.Default.Send(new AppNotificationMessage(true, new ExportFontFileResult(true, file)));
            }
        }

        internal static Task ExportCollectionToFolderAsync(
            IList<InstalledFont> fontList,
            Action<string> callback = null)
        {
            var fonts = fontList.SelectMany(f => f.Variants).ToList();
            return ExportFontsToFolderAsync(fonts, callback);
        }

        internal static async Task ExportFontsToFolderAsync(
            List<FontVariant> fonts,
            Action<string> callback = null)
        {
            if (await PickFolderAsync() is StorageFolder folder)
            {
                callback?.Invoke($"0%");
                await Task.Run(async () =>
                {
                    ExportNamingScheme scheme = ResourceHelper.AppSettings.ExportNamingScheme;
                    int c = 0;

                    var grouped = GetGrouped(fonts); ;
                    foreach (var group in grouped)
                    {
                        string fileName = GetFileName(group, scheme);
                        StorageFile file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);
                        await TryWriteToFileAsync(group.First(), file).ConfigureAwait(false);

                        c++;
                        callback?.Invoke($"{((double)c / (double)grouped.Count) * 100:0}%");
                    }
                });

                WeakReferenceMessenger.Default.Send(new AppNotificationMessage(true, new ExportFontFileResult(folder, true)));
            }
        }

        private static async Task<bool> TryWriteToFileAsync(FontVariant font, StorageFile file)
        {
            try
            {
                using IRandomAccessStream s = await file.OpenAsync(FileAccessMode.ReadWrite).AsTask().ConfigureAwait(false);
                s.Size = 0;

                using IOutputStream o = s.GetOutputStreamAt(0);
                await DirectWrite.WriteToStreamAsync(font.Face, o).AsTask().ConfigureAwait(false);
                await s.FlushAsync(); // using statements force synchronous flushes
                return true;
            }
            catch { }

            return false;
        }

        private static string GetFileName(IGrouping<string, FontVariant> group, ExportNamingScheme scheme)
        {
            string fileName = null;
            string ext = ".ttf";

            var src = group.Key;
            if (!string.IsNullOrWhiteSpace(src))
            {
                var strsrc = Path.GetExtension(src);
                if (!string.IsNullOrWhiteSpace(strsrc))
                    ext = strsrc;
            }

            if (scheme == ExportNamingScheme.System && !string.IsNullOrWhiteSpace(src))
                fileName = src;

            if (scheme is ExportNamingScheme.Optimised && FontFinder.ImportFormats.Contains(ext) is false)
                ext = ".ttf";

            var font = Utils.GetDefaultVariant(group.ToList());

            if (string.IsNullOrWhiteSpace(fileName))
                fileName = $"{font.GetFullName().Trim()}{ext}";

            return $"{Utils.Humanise(Path.GetFileNameWithoutExtension(fileName), false)}{Path.GetExtension(fileName).ToLower()}";
        }

        private static string GetFileName(FontVariant font, ExportNamingScheme scheme)
        {
            string fileName = null;
            string ext = ".ttf";

            var src = DirectWrite.GetFileName(font.Face);
            if (!string.IsNullOrWhiteSpace(src))
            {
                var strsrc = Path.GetExtension(src);
                if (!string.IsNullOrWhiteSpace(strsrc))
                    ext = strsrc;
            }

            if (scheme == ExportNamingScheme.System && !string.IsNullOrWhiteSpace(src))
                fileName = src;

            if (scheme is ExportNamingScheme.Optimised && FontFinder.ImportFormats.Contains(ext) is false)
                ext = ".ttf";

            if (string.IsNullOrWhiteSpace(fileName))
                fileName = $"{font.FamilyName.Trim()} {font.PreferredName.Trim()}{ext}";

            return $"{Utils.Humanise(Path.GetFileNameWithoutExtension(fileName), false)}{Path.GetExtension(fileName).ToLower()}";
        }
    }
}
