using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharacterMap.Services
{
    public interface IDialogService
    {
        void ShowMessageBox(string message, string title);
        Task ShowMessageAsync(string message, string title);
    }
}
