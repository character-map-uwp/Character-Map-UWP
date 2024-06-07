namespace CharacterMap.Models;

public class ModalClosedMessage { }

public record class CollectionUpdatedArgs(IReadOnlyList<InstalledFont> Fonts, UserFontCollection Collection, bool IsAdd)
{
    public string GetTitle()
    {
        if (Fonts.Count == 1 && Fonts[0] is InstalledFont font)
            return font.Name;
        else
            return $"{Fonts.Count} fonts";
    }

    public string GetMessage()
    {
        if (Fonts.Count == 1 && Fonts[0] is InstalledFont font)
        {
            if (IsAdd)
                return $"{font.Name} was added to the \"{Collection.Name}\" collection";
            else
                return $"{font.Name} was removed from the \"{Collection.Name}\" collection";
        }
        else
        {
            if (IsAdd)
                return $"{Fonts.Count} fonts were added to the \"{Collection.Name}\" collection";
            else
                return $"{Fonts.Count} fonts were removed from the \"{Collection.Name}\" collection";
        }

    }
}

public class FontListCreatedMessage { }

public record class ImportMessage(FontImportResult Result);

public class CollectionsUpdatedMessage
{
    public UserFontCollection SourceCollection { get; set; }
}

public class CollectionRequestedMessage
{
    public IFontCollection Collection { get; }

    public bool Handled { get; set; }

    public CollectionRequestedMessage(IFontCollection sourceCollection)
    {
        Collection = sourceCollection;
    }
}

public class PrintRequestedMessage { }

public class ExportRequestedMessage { }

public class RampOptionsUpdatedMessage { }

public class EditSuggestionsRequested { }

public class AdvancedOptionsRequested { }

public class ToggleCompactOverlayMessage { }

public record class AppSettingsChangedMessage(string PropertyName);

public record class AppNotificationMessage(bool Local, object Data, int DurationInMilliseconds = 0);
   

public enum DevValueType
{
    Char,
    Glyph,
    FontIcon,
    PathIcon,
    UnicodeValue,
}

public enum CopyDataType
{
    Text,
    PNG,
    SVG
}


public class CopyToClipboardMessage
{
    public DevValueType CopyType { get; }
    public CopyDataType DataType { get; }
    public Character RequestedItem { get; }
    public CanvasTextLayoutAnalysis Analysis { get; }

    public ExportStyle Style { get; set; }

    public CopyToClipboardMessage(Character c)
    {
        CopyType = DevValueType.Char;
        RequestedItem = c;
        Analysis = null;
    }

    public CopyToClipboardMessage(DevValueType type, Character requested, CanvasTextLayoutAnalysis ca, CopyDataType dataType = CopyDataType.Text)
    {
        CopyType = type;
        RequestedItem = requested;
        Analysis = ca;
        DataType = dataType;
    }
}
