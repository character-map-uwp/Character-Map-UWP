namespace CharacterMap.Services;

public class AddToCollectionResult
{
    public AddToCollectionResult(bool success, IReadOnlyList<CMFontFamily> fonts, IFontCollection collection)
    {
        Success = success;
        Fonts = fonts;
        if (fonts is not null && fonts.Count == 1)
            Font = fonts[0];
        Collection = collection;
    }

    public CMFontFamily Font { get; }
    public IReadOnlyList<CMFontFamily> Fonts { get; }
    public bool Success { get; }
    public IFontCollection Collection { get; }

    public string GetTitle()
    {
        if (Fonts is null) 
            return string.Empty;

        if (Fonts.Count == 1 && Fonts[0] is CMFontFamily font)
            return font.Name;
        else
            return $"{Fonts.Count} fonts";
    }

    public string GetMessage()
    {
        if (Fonts is null && Font is null)
            return string.Empty;

        if (Font is null && Fonts is not null)
            return $"{Fonts.Count} fonts have been added to {Collection.Name}";
        else if (Collection is UserFontCollection u && u.IsSystemSymbolCollection)
            return Localization.Get("NotificationAddedToCollection", Font.Name, Localization.Get("OptionSymbolFonts/Text"));
        else
            return Localization.Get("NotificationAddedToCollection", Font.Name, Collection.Name);
    }
}
