//using CharacterMapCX;
//using SQLite;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Windows.ApplicationModel;
//using Windows.Storage;

//namespace CharacterMap.Services
//{
//    public class FontTable
//    {
//#if DEBUG
//        [Indexed]
//#endif
//        public string Id { get; set; }

//#if DEBUG
//        [Indexed]
//#endif
//        public string FamilyName { get; set; }
//        public string FaceName { get; set; }

//        public DWriteFontSource Source { get; set; }
//        public bool IsColour { get; set; }
//        public bool IsVariable { get; set; }
//    }

//    internal class FontCacheService
//    {
//        public SQLiteConnection Connection { get; }

//        public FontCacheService()
//        {
//            string path = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "fontcache.db");
//            Connection = new SQLiteConnection(path, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite);
//            Connection.EnableWriteAheadLogging().CreateTable<FontTable>();
//        }
//    }
//}
