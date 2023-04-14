using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Provider;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;

namespace CharacterMap.Services
{
    public interface ICollectionProvider
    {
        Task  StoreMigrationAsync(List<UserFontCollection> collections);
        /// <summary>
        /// Returns an ordered List of collection
        /// </summary>
        /// <returns></returns>
        Task<List<UserFontCollection>> LoadCollectionsAsync();
        Task SaveCollectionAsync(UserFontCollection collection);
        Task<bool> DeleteCollectionAsync(UserFontCollection collection);
        Task FlushAsync();
    }

    public class UserCollectionsService
    {
        private UserFontCollection _symbolCollection = null;
        public UserFontCollection SymbolCollection
        {
            get => _symbolCollection;
            set { value.IsSystemSymbolCollection = true; _symbolCollection = value; }
        }

        public List<UserFontCollection> Items { get; private set; } = new ();

        private ICollectionProvider _provider { get; }

        public UserCollectionsService()
        {
            _provider = new SQLiteCollectionProvider();
        }

        public bool IsSymbolFont(InstalledFont font)
        {
            return font != null && (font.IsSymbolFont || SymbolCollection.Fonts.Contains(font.Name));
        }

        public Task FlushAsync()
        {
            return _provider.FlushAsync();
        }

        public async Task LoadCollectionsAsync()
        {
            if (Items.Count > 0)
                return;

            if (ResourceHelper.AppSettings.HasSQLiteCollections is false)
                await UpgradeToSQLiteAsync().ConfigureAwait(false);

            List<UserFontCollection> collections = new();

            await Task.Run(async () =>
            {
                var cols = await _provider.LoadCollectionsAsync().ConfigureAwait(false);
                foreach (var c in cols)
                {
                    if (c.Name == "Symbol")
                        SymbolCollection = c;
                    else
                        collections.Add(c);
                }

                if (SymbolCollection == null)
                    SymbolCollection = await CreateCollectionAsync("Symbol", true).ConfigureAwait(false);
            });

            Items.Clear();
            Items.AddRange(collections);
        }

        public UserFontCollection TryGetCollection(string name)
        {
            return Items.FirstOrDefault(i => string.CompareOrdinal(i.Name, name) > 0);
        }

        public async Task<UserFontCollection> CreateCollectionAsync(string name, bool symbol = false)
        {
            if (!symbol && name == "Symbol")
                name = "My Symbols";

            UserFontCollection collection = new() { Name = name };
            await SaveCollectionAsync(collection).ConfigureAwait(false);

            if (!symbol)
            {
                Items.Add(collection);
                Items = Items.OrderBy(i => i.Name).ToList();
            }

            return collection;
        }

        public async Task DeleteCollectionAsync(UserFontCollection collection)
        {
            if (await _provider.DeleteCollectionAsync(collection))
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
            return _provider.SaveCollectionAsync(collection);
        }

        public Task<AddToCollectionResult> AddToCollectionAsync(InstalledFont font, UserFontCollection collection)
        {
            return AddToCollectionAsync(new List<InstalledFont> { font }, collection);
        }

        public async Task<AddToCollectionResult> AddToCollectionAsync(IList<InstalledFont> fonts, UserFontCollection collection, Action onChanged = null)
        {
            bool changed = false;

            foreach (var font in fonts)
            {
                if (font is not null && collection.Fonts.Add(font.Name))
                    changed = true;
            }

            if (changed)
            {
                await SaveCollectionAsync(collection);
                onChanged?.Invoke();
                return new AddToCollectionResult(true, fonts, collection);
            }

            return new AddToCollectionResult(false, null, null);
        }

        public Task RemoveFromCollectionAsync(InstalledFont font, UserFontCollection collection)
        {
            return RemoveFromCollectionAsync(new List<InstalledFont> { font }, collection);
        }

        public Task RemoveFromCollectionAsync(IList<InstalledFont> fonts, UserFontCollection collection)
        {
            bool removed = false;
            foreach (var font in fonts)
                if (collection.Fonts.Remove(font.Name))
                    removed = true;

            if (removed)
                return SaveCollectionAsync(collection);

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




        //------------------------------------------------------
        //
        // Legacy Format Conversions
        //
        //------------------------------------------------------

        [Obsolete]
        private IAsyncOperation<StorageFolder> GetCollectionsFolderAsync()
        {
            return ApplicationData.Current.LocalCacheFolder.CreateFolderAsync("Collections", CreationCollisionOption.OpenIfExists);
        }

        [Obsolete]
        private async Task<UserFontCollection> LoadFileCollectionAsync(StorageFile file)
        {
            string name;
            HashSet<string> items = new ();

            using (var stream = await file.OpenStreamForReadAsync().ConfigureAwait(false))
            using (var reader = new StreamReader(stream))
            {
                name = reader.ReadLine();
                while (!reader.EndOfStream)
                    items.Add(reader.ReadLine());
            }

            return new UserFontCollection { Fonts = items, Name = name };
        }

        private async Task UpgradeToSQLiteAsync()
        {
            List<UserFontCollection> collections = new();

            // 1. Load Existing collections
            await Task.Run(async () =>
            {
                var folder = await GetCollectionsFolderAsync().AsTask().ConfigureAwait(false);
                var files = await folder.GetFilesAsync().AsTask().ConfigureAwait(false);

                if (files.Any())
                {
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
                                    collection = await LoadFileCollectionAsync(file).ConfigureAwait(false);

                                return collection;
                            }
                            catch
                            {
                                // Possibly corrupted. What to do? Delete file?
                                return null;
                            }
                            finally
                            {
                                await file.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
                            }
                        });
                    }).ToList();

                    await Task.WhenAll(tasks).ConfigureAwait(false);

                    collections = tasks.Select(t => t.Result).Where(t => t != null).OrderBy(c => c.Name).ToList();
                }

                await _provider.StoreMigrationAsync(collections);
            });

            
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

            var collection = new UserFontCollection { Fonts = items, Name = name };
            var newFile = await folder.CreateFileAsync(file.DisplayName, CreationCollisionOption.OpenIfExists).AsTask().ConfigureAwait(false);
            await SaveCollectionAsync(collection).ConfigureAwait(false);
            await file.DeleteAsync().AsTask().ConfigureAwait(false);
            return collection;
        }
    }
}
