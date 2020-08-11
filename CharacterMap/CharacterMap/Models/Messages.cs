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
    }

    public class PrintRequestedMessage
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

    public class SaveAsPictureMessage
    {
        public enum SaveAs { PNG, SVG}
        public SaveAs Save { get; }
        public Character Character { get; }
        public CanvasTextLayoutAnalysis Analysis { get; }
        public ExportStyle Style { get; }

        public SaveAsPictureMessage(Character c, CanvasTextLayoutAnalysis ctla, SaveAs s, ExportStyle style)
        {
            Save = s;
            Character = c;
            Analysis = ctla;
            Style = style;
        }
    }

    public class CopyToClipboardMessage
    {
        public enum MessageType { Char, DevGlyph, DevFont, DevPath }
        public MessageType CopyItem { get; }
        public Character RequestedItem { get; }
        public CanvasTextLayoutAnalysis Analysis { get; }

        public CopyToClipboardMessage(Character c)
        {
            CopyItem = MessageType.Char;
            RequestedItem = c;
            Analysis = null;
        }

        public CopyToClipboardMessage(MessageType copy, Character requested, CanvasTextLayoutAnalysis ca)
        {
            CopyItem = copy;
            RequestedItem = requested;
            Analysis = ca;
        }
    }

    public class ToggleCompactOverlayMessage { }
}
