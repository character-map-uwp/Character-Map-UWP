using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;

namespace CharacterMap.Activation
{
    public class FileActivationHandler : ActivationHandler<FileActivatedEventArgs>
    {
        protected override Task HandleInternalAsync(FileActivatedEventArgs args)
        {
            return Task.CompletedTask;
        }
    }
}
