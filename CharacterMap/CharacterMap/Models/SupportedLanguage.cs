using System.Globalization;

namespace CharacterMap.Models;

public record SupportedLanguage
{
    public string LanguageID { get; init; }
    public string LanguageName { get; init; }

    public SupportedLanguage()
    {
        LanguageName = "";
        LanguageID = "";
    }

    public SupportedLanguage(string ID)
    {
        CultureInfo culture = new(ID);
        LanguageID = culture.Name;
        LanguageName = culture.NativeName;
    }

    public override string ToString() => LanguageName;

    public static SupportedLanguage DefaultLanguage { get; } = new("en-US");
    public static SupportedLanguage SystemLanguage { get; } = new()
    {
        LanguageID = "",
        LanguageName = Localization.Get("UseSystemLanguage")
    };
}
