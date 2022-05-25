using CharacterMap.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;

namespace CharacterMap.Models
{
    public record FolderOpenOptions
    {
        public IStorageItem Root { get; init; }
        public bool Recursive { get; init; }
        public bool AllowZip { get; init; }
        public CancellationToken? Token { get; init; }
        public Action<int> Callback { get; init; }

        public bool IsCancelled => Token is not null && Token.HasValue && Token.Value.IsCancellationRequested;
        public void Increment(int count) => Callback?.Invoke(count);
    }

    public class FolderContents
    {
        public FolderContents(IStorageItem source, StorageFolder tempFolder)
        {
            Source = source;
            SourceFolder = source as StorageFolder;
            TempFolder = tempFolder;
            FontCache = new();
        }

        public Dictionary<string, InstalledFont> FontCache { get; }

        /// <summary>
        /// The original StorageItem source the content was loaded from. 
        /// Could be a StorageFolder or a single .zip StorageFile
        /// </summary>
        public IStorageItem Source { get; }

        /// <summary>
        /// Original folder chosen by users
        /// </summary>
        public StorageFolder SourceFolder { get; }

        /// <summary>
        /// Temporary folder the fonts were copied into to be able to
        /// be loaded by the XAML framework
        /// </summary>
        public StorageFolder TempFolder { get; }

        public IReadOnlyList<InstalledFont> Fonts { get; private set; }

        public void UpdateFontSet()
        {
            Fonts = FontFinder.CreateFontList(FontCache);
        }

        public Task LaunchSourceAsync()
        {
            if (SourceFolder is not null)
                return Launcher.LaunchFolderAsync(SourceFolder).AsTask();
            else if (Source is StorageFile file)
                return Launcher.LaunchFileAsync(file).AsTask();

            return Task.CompletedTask;
        }
    }
}
