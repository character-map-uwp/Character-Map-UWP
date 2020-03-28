using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace CharacterMap.Core
{
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
}
