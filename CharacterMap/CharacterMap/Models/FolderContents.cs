using CharacterMap.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace CharacterMap.Models
{
    public class FolderContents
    {
        public FolderContents(StorageFolder sourceFolder, StorageFolder tempFolder, IReadOnlyList<InstalledFont> fonts)
        {
            SourceFolder = sourceFolder;
            TempFolder = tempFolder;
            Fonts = fonts;
        }

        /// <summary>
        /// Original folder chosen by users
        /// </summary>
        public StorageFolder SourceFolder { get; }

        /// <summary>
        /// Temporary folder the fonts were copied into to be able to
        /// be loaded by the XAML framework
        /// </summary>
        public StorageFolder TempFolder { get; }

        public IReadOnlyList<InstalledFont> Fonts { get; }
    }
}
