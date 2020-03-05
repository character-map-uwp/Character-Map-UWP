using Newtonsoft.Json;
using System.Collections.Generic;
using Windows.Storage;

namespace CharacterMap.Models
{
    public class UserFontCollection
    {
        [JsonIgnore]
        public bool IsSystemSymbolCollection { get; set; }
        [JsonIgnore]
        public StorageFile File { get; set; }
        public string Name { get; set; }
        public HashSet<string> Fonts { get; set; } = new HashSet<string>();
    }
}
