using CharacterMap.Core;
using CharacterMap.Provider;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Core
{
    public class UnicodeGlyphData
    {
        [PrimaryKey]
        public int UnicodeIndex { get; set; }

        [Indexed]
        [MaxLength(5)]
        public string UnicodeHex { get; set; }

        [Indexed]
        [MaxLength(2)]
        public string UnicodeGroup { get; set;}

        public string Description { get; set; }
    }

}
