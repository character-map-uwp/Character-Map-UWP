using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CharacterMap.ViewModels
{
    public class ViewModelBase : ObservableObject
    {
        public bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
            => base.SetProperty(ref field, value, propertyName);

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            OnPropertyChangeNotified(e.PropertyName);
        }

        protected virtual void OnPropertyChangeNotified(string propertyName) { }

        public WeakReferenceMessenger Messenger 
            => WeakReferenceMessenger.Default;
    }
}
