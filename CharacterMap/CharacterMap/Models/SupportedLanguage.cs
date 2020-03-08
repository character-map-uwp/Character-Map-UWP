using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharacterMap.Models
{
    public class SupportedLanguage
    {
        public string LanguageID;
        public string LanguageName;

        public SupportedLanguage()
        {
            LanguageName = "";
            LanguageID = "";
        }

        public SupportedLanguage(string ID)
        {
            LanguageID = new CultureInfo(ID).Name;
            LanguageName = new CultureInfo(ID).NativeName;
        }

        public override string ToString() => LanguageName;

        public static SupportedLanguage DefaultLanguage => new SupportedLanguage("en-US");
        public static SupportedLanguage SystemLanguage => new SupportedLanguage()
        {
            LanguageID = "",
            LanguageName = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView().GetString("UseSystemLanguage")
        };
    }
}
