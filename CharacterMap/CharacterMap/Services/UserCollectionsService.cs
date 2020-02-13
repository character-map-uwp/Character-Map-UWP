using CharacterMap.Core;
using CharacterMap.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;

namespace CharacterMap.Services
{
    public class UserFontCollection
    {
        [JsonIgnore]
        public StorageFile File { get; set; }
        public string Name { get; set; }
        public HashSet<string> Fonts { get; set; } = new HashSet<string>();
    }

    public class UserCollectionsService
    {
        public UserFontCollection SymbolCollection { get; private set; }
        public List<UserFontCollection> Items { get; private set; } = new List<UserFontCollection>();

        private StorageFolder _collectionsFolder;

        public async Task LoadCollectionsAsync()
        {
            List<UserFontCollection> collections = new List<UserFontCollection>();

            await Task.Run(async () =>
            {
                var folder = _collectionsFolder = await GetCollectionsFolderAsync().AsTask().ConfigureAwait(false);
                var files = await folder.GetFilesAsync().AsTask().ConfigureAwait(false);

                foreach (var file in files)
                {
                    try
                    {
                        UserFontCollection collection = await Json.ReadAsync<UserFontCollection>(file).ConfigureAwait(false);
                        collection.File = file;
                        if (file.DisplayName != "Symbol")
                        {
                            collections.Add(collection);
                        }
                        else
                        {
                            SymbolCollection = collection;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Possibly corrupted. What to do? Delete file?
                    }
                }

                if (SymbolCollection == null)
                {
                    SymbolCollection = await CreateCollectionAsync("Symbol", "Symbol").ConfigureAwait(false);
                }

                collections = collections.OrderBy(c => c.Name).ToList();
            });
            
            foreach (var item in collections)
                Items.Add(item);
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
                $"{fileName ?? Guid.NewGuid().ToString()}.json", CreationCollisionOption.GenerateUniqueName).AsTask().ConfigureAwait(false);
            var collection = new UserFontCollection { Name = name, File = file };
            await SaveCollectionAsync(collection).ConfigureAwait(false);
            Items.Add(collection);
            Items = Items.OrderBy(i => i.Name).ToList();
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
            return Json.WriteAsync(collection.File, collection);
        }

        public Task AddToCollectionAsync(InstalledFont font, UserFontCollection collection)
        {
            if (!collection.Fonts.Contains(font.Name))
            {
                collection.Fonts.Add(font.Name);
                return SaveCollectionAsync(collection);
            }

            return Task.CompletedTask;
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
    }
}
