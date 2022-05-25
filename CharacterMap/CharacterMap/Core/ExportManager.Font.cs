using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMapCX;
using Microsoft.Toolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

            if (DirectWrite.IsFontLocal(variant.FontFace))
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

        internal static Task ExportCollectionAsZipAsync(IList<InstalledFont> fontList, UserFontCollection selectedCollection)
        {
            var fonts = fontList.SelectMany(f => f.Variants).ToList();
            return ExportFontsAsZipAsync(fonts, selectedCollection.Name);
        }

        internal static async Task ExportFontsAsZipAsync(List<FontVariant> fonts, string name)
        {
            if (await PickFileAsync(name, "ZIP", new[] { ".zip" }) is StorageFile file)
            {
                await Task.Run(async () =>
                {
                    ExportNamingScheme scheme = ResourceHelper.AppSettings.ExportNamingScheme;

                    using var i = await file.OpenStreamForWriteAsync();
                    i.SetLength(0);

                    using ZipArchive z = new(i, ZipArchiveMode.Create);
                    foreach (var font in fonts)
                    {
                        if (DirectWrite.IsFontLocal(font.FontFace))
                        {
                            string fileName = GetFileName(font, scheme);
                            ZipArchiveEntry entry = z.CreateEntry(fileName);
                            using IOutputStream s = entry.Open().AsOutputStream();
                            await DirectWrite.WriteToStreamAsync(font.FontFace, s);
                        }
                    }

                    await i.FlushAsync();
                });

                WeakReferenceMessenger.Default.Send(new AppNotificationMessage(true, new ExportFontFileResult(true, file)));
            }
        }

        internal static Task ExportCollectionToFolderAsync(IList<InstalledFont> fontList)
        {
            var fonts = fontList.SelectMany(f => f.Variants).ToList();
            return ExportFontsToFolderAsync(fonts);
        }

        internal static async Task ExportFontsToFolderAsync(List<FontVariant> fonts)
        {
            if (await PickFolderAsync() is StorageFolder folder)
            {
                await Task.Run(async () =>
                {
                    ExportNamingScheme scheme = ResourceHelper.AppSettings.ExportNamingScheme;

                    foreach (var font in fonts)
                    {
                        if (DirectWrite.IsFontLocal(font.FontFace))
                        {
                            string fileName = GetFileName(font, scheme);
                            StorageFile file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);
                            await TryWriteToFileAsync(font, file).ConfigureAwait(false);
                        }
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
                await DirectWrite.WriteToStreamAsync(font.FontFace, o).AsTask().ConfigureAwait(false);
                await s.FlushAsync(); // using statements force synchronous flushes
                return true;
            }
            catch { }

            return false;
        }

        private static string GetFileName(FontVariant font, ExportNamingScheme scheme)
        {
            string fileName = null;
            string ext = ".ttf";

            var src = DirectWrite.GetFileName(font.FontFace);
            if (!string.IsNullOrWhiteSpace(src))
            {
                var strsrc = Path.GetExtension(src);
                if (!string.IsNullOrWhiteSpace(strsrc))
                    ext = strsrc;
            }

            if (string.IsNullOrEmpty(ext))

            if (scheme == ExportNamingScheme.System)
                fileName = src;

            if (string.IsNullOrWhiteSpace(fileName))
                fileName = $"{font.FamilyName.Trim()} {font.PreferredName.Trim()}{ext}";

            return $"{Utils.Humanise(Path.GetFileNameWithoutExtension(fileName), false)}{Path.GetExtension(fileName).ToLower()}";
        }
    }
}
