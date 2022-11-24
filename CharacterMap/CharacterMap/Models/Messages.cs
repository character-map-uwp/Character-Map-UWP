using CharacterMap.Core;
using CharacterMapCX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace CharacterMap.Models
{
    public class ModalClosedMessage { }

    public class CollectionUpdatedArgs
    {
        public InstalledFont Font { get; }
        public UserFontCollection Collection { get; }
        public bool IsAdd { get; }

        public CollectionUpdatedArgs(InstalledFont font, UserFontCollection collection, bool isAdd)
        {
            Font = font;
            Collection = collection;
            IsAdd = isAdd;
        }

        public string GetMessage()
        {
            if (IsAdd)
                return $"{Font.Name} was added to the \"{Collection.Name}\" collection";
            else
                return $"{Font.Name} was removed from the \"{Collection.Name}\" collection";
        }
    }

    public class FontListCreatedMessage { }

    public class ImportMessage
    {
        public ImportMessage(FontImportResult result)
        {
            Result = result;
        }

        public FontImportResult Result { get; }
    }

    public class CollectionsUpdatedMessage
    {
        public UserFontCollection SourceCollection { get; set; }
    }

    public class CollectionRequestedMessage
    {
        public UserFontCollection Collection { get; }

        public bool Handled { get; set; }

        public CollectionRequestedMessage(UserFontCollection sourceCollection)
        {
            Collection = sourceCollection;
        }
    }

    public class PrintRequestedMessage
    {
    }

    public class ExportRequestedMessage
    {
    }

    public class AppSettingsChangedMessage
    {
        public AppSettingsChangedMessage(string propertyName)
        {
            PropertyName = propertyName;
        }

        public string PropertyName { get; }
    }

    public class AppNotificationMessage
    {
        public AppNotificationMessage(bool local, object data, int durationMs = 0)
        {
            Local = local;
            Data = data;
            DurationInMilliseconds = durationMs;
        }

        public bool Local { get; }
        public object Data { get; }
        public int DurationInMilliseconds { get; }
    }

    public enum DevValueType
    {
        Char,
        Glyph,
        FontIcon,
        PathIcon,
        UnicodeValue,
    }


    public class CopyToClipboardMessage
    {
        public DevValueType CopyType { get; }
        public Character RequestedItem { get; }
        public CanvasTextLayoutAnalysis Analysis { get; }

        public CopyToClipboardMessage(Character c)
        {
            CopyType = DevValueType.Char;
            RequestedItem = c;
            Analysis = null;
        }

        public CopyToClipboardMessage(DevValueType type, Character requested, CanvasTextLayoutAnalysis ca)
        {
            CopyType = type;
            RequestedItem = requested;
            Analysis = ca;
        }
    }

    public class ToggleCompactOverlayMessage { }
}
