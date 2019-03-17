using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

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
}
