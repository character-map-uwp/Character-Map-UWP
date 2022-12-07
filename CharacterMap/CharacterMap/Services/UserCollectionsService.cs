using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;

namespace CharacterMap.Services
{
    public class AddToCollectionResult
    {
        public AddToCollectionResult(bool success, InstalledFont font, UserFontCollection collection)
        {
            Success = success;
            Font = font;
            Collection = collection;
        }

        public InstalledFont Font { get; }
        public bool Success { get; }
        public UserFontCollection Collection { get; }

        public string GetMessage()
        {
            if (Collection.IsSystemSymbolCollection)
                return Localization.Get("NotificationAddedToCollection", Font.Name, Localization.Get("OptionSymbolFonts/Text"));
            else
                return Localization.Get("NotificationAddedToCollection", Font.Name, Collection.Name);
        }
    }

    public class UserCollectionsService
    {
        private UserFontCollection _symbolCollection = null;
        public UserFontCollection SymbolCollection
        {
            get => _symbolCollection;
            set { value.IsSystemSymbolCollection = true; _symbolCollection = value; }
        }
        public List<UserFontCollection> Items { get; private set; } = new List<UserFontCollection>();

        private StorageFolder _collectionsFolder;

        public bool IsSymbolFont(InstalledFont font)
        {
            return font != null && (font.IsSymbolFont || SymbolCollection.Fonts.Contains(font.Name));
        }

        public async Task LoadCollectionsAsync()
        {
            List<UserFontCollection> collections = new List<UserFontCollection>();

            if (Items.Count > 0)
                return;

            await Task.Run(async () =>
            {
                var folder = _collectionsFolder = await GetCollectionsFolderAsync().AsTask().ConfigureAwait(false);
                var files = await folder.GetFilesAsync().AsTask().ConfigureAwait(false);

                var tasks = files.Select(file =>
                {
                    return Task.Run(async () =>
                    {
                        try
                        {
                            UserFontCollection collection;
                            if (file.FileType == ".json")
                                collection = await ConvertFromLegacyFormatAsync(file, folder).ConfigureAwait(false);
                            else
                                collection = await LoadCollectionAsync(file).ConfigureAwait(false);

                            return collection;
                        }
                        catch
                        {
                            // Possibly corrupted. What to do? Delete file?
                            return null;
                        }
                    });
                }).ToList();

                await Task.WhenAll(tasks).ConfigureAwait(false);

                foreach (var task in tasks)
                {
                    if (task.Result is UserFontCollection collection)
                    {
                        if (collection.File.DisplayName != "Symbol")
                        {
                            collections.Add(collection);
                        }
                        else
                        {
                            SymbolCollection = collection;
                        }
                    }
                }

                if (SymbolCollection == null)
                {
                    SymbolCollection = await CreateCollectionAsync("Symbol", "Symbol").ConfigureAwait(false);
                }

                collections = collections.OrderBy(c => c.Name).ToList();
            });


            Items.Clear();
            Items.AddRange(collections);
        }

        public bool DoesCollectionExist(string name)
        {
            return TryGetCollection(name) != null;
        }

        public UserFontCollection TryGetCollection(string name)
        {
            return Items.FirstOrDefault(i => string.CompareOrdinal(i.Name, name) > 0);
        }

        public async Task<UserFontCollection> CreateCollectionAsync(string name, string fileName = null)
        {
            var file = await _collectionsFolder.CreateFileAsync(
                $"{fileName ?? Guid.NewGuid().ToString()}", CreationCollisionOption.GenerateUniqueName).AsTask().ConfigureAwait(false);

            var collection = new UserFontCollection { Name = name, File = file };
            await SaveCollectionAsync(collection).ConfigureAwait(false);

            if (fileName == null)
            {
                Items.Add(collection);
                Items = Items.OrderBy(i => i.Name).ToList();
            }

            return collection;
        }

        public async Task DeleteCollectionAsync(UserFontCollection collection)
        {
            await collection.File.DeleteAsync().AsTask().ConfigureAwait(false);
            Items.Remove(collection);
        }

        public async Task RenameCollectionAsync(string name, UserFontCollection collection)
        {
            collection.Name = name;
            await SaveCollectionAsync(collection).ConfigureAwait(false);
            Items = Items.OrderBy(i => i.Name).ToList();
        }

        public Task SaveCollectionAsync(UserFontCollection collection)
        {
            return Task.Run(async () =>
            {
                using var stream = await collection.File.OpenStreamForWriteAsync().ConfigureAwait(false);
                using var reader = new StreamWriter(stream);
                stream.SetLength(0);
                reader.Write(collection.Name);
                foreach (var item in collection.Fonts)
                {
                    reader.WriteLine();
                    reader.Write(item);
                }
            });

        }

        public async Task<AddToCollectionResult> AddToCollectionAsync(InstalledFont font, UserFontCollection collection)
        {
            if (font is null || !collection.Fonts.Contains(font.Name))
            {
                if (font is not null)
                    collection.Fonts.Add(font.Name);

                await SaveCollectionAsync(collection);
                return new AddToCollectionResult(true, font, collection);
            }

            return new AddToCollectionResult(false, null, null);
        }

        public Task RemoveFromCollectionAsync(InstalledFont font, UserFontCollection collection)
        {
            if (collection.Fonts.Remove(font.Name))
            {
                return SaveCollectionAsync(collection);
            }

            return Task.CompletedTask;
        }

        public async Task RemoveFromAllCollectionsAsync(InstalledFont font)
        {
            if (SymbolCollection.Fonts.Contains(font.Name))
                await RemoveFromCollectionAsync(font, SymbolCollection).ConfigureAwait(false);

            foreach (var collection in Items)
            {
                if (collection.Fonts.Contains(font.Name))
                    await RemoveFromCollectionAsync(font, collection).ConfigureAwait(false);
            }
        }

        private IAsyncOperation<StorageFolder> GetCollectionsFolderAsync()
        {
            return ApplicationData.Current.LocalCacheFolder.CreateFolderAsync("Collections", CreationCollisionOption.OpenIfExists);
        }

        private async Task<UserFontCollection> LoadCollectionAsync(StorageFile file)
        {
            string name;
            HashSet<string> items = new HashSet<string>();

            using (var stream = await file.OpenStreamForReadAsync().ConfigureAwait(false))
            using (var reader = new StreamReader(stream))
            {
                name = reader.ReadLine();
                while (!reader.EndOfStream)
                    items.Add(reader.ReadLine());
            }

            return new UserFontCollection { File = file, Fonts = items, Name = name };
        }

        private async Task<UserFontCollection> ConvertFromLegacyFormatAsync(StorageFile file, StorageFolder folder)
        {
            // Legacy Collection format used .json files.
            // JSON lib was dropped to improve perf and reduce RAM,
            // so manually parse.

            string name;
            HashSet<string> items = new HashSet<string>();

            using (var stream = await file.OpenStreamForReadAsync().ConfigureAwait(false))
            using (var reader = new StreamReader(stream))
            {
                string s = reader.ReadToEnd();

                //{"Name":"
                s = s.Remove(0, 9);
                var i = s.IndexOf("\"");
                name = s.Substring(0, i);
                s = s.Remove(0, s.IndexOf("[") + 1);

                while (true)
                {
                    var c = s.IndexOf(",");
                    if (c >= 0)
                    {
                        var n = s.Substring(1, c - 2);
                        items.Add(n);
                        s = s.Remove(0, c + 1);
                    }
                    else
                    {
                        s = s.Replace("]}", string.Empty);
                        if (s.Length > 2)
                        {
                            s = s.Substring(1, s.Length - 2);
                            if (!string.IsNullOrEmpty(s))
                                items.Add(s);
                        }

                        break;
                    }
                }
            }

            var collection = new UserFontCollection { File = file, Fonts = items, Name = name };
            var newFile = await folder.CreateFileAsync(file.DisplayName, CreationCollisionOption.OpenIfExists).AsTask().ConfigureAwait(false);
            collection.File = newFile;
            await SaveCollectionAsync(collection).ConfigureAwait(false);
            await file.DeleteAsync().AsTask().ConfigureAwait(false);
            return collection;
        }
    }
}
