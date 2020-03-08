using System;
using System.Collections.Generic;
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
            LanguageID = new System.Globalization.CultureInfo(ID).Name;
            LanguageName = new System.Globalization.CultureInfo(ID).DisplayName;
        }

        public override string ToString() => LanguageName;

        public static SupportedLanguage DefaultLanguage => new SupportedLanguage("en-US");
    }
}
