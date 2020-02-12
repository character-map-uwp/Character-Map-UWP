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
        public ObservableCollection<UserFontCollection> Items { get; } = new ObservableCollection<UserFontCollection>();

        public async Task LoadCollectionsAsync()
        {
            List<UserFontCollection> collections = new List<UserFontCollection>();

            await Task.Run(async () =>
            {
                var folder = await GetCollectionsFolderAsync().AsTask().ConfigureAwait(false);
                var files = await folder.GetFilesAsync().AsTask().ConfigureAwait(false);

                foreach (var file in files)
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
                if (SymbolCollection == null)
                {
                    SymbolCollection = await CreateCollectionAsync("Symbol", "Symbol");
                }
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
            var folder = await GetCollectionsFolderAsync();
            var file = await folder.CreateFileAsync($"{fileName ?? Guid.NewGuid().ToString()}.json");
            var collection = new UserFontCollection { Name = name, File = file };
            await SaveCollectionAsync(collection);
            Items.Add(collection);
            return collection;
        }

        public async Task DeleteCollectionAsync(UserFontCollection collection)
        {
            await collection.File.DeleteAsync();
            Items.Remove(collection);
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

        private IAsyncOperation<StorageFolder> GetCollectionsFolderAsync()
        {
            return ApplicationData.Current.LocalCacheFolder.CreateFolderAsync("Collections", CreationCollisionOption.OpenIfExists);
        }
    }
}
