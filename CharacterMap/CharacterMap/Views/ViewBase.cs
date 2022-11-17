using CharacterMap.Annotations;
using CharacterMap.Core;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Views
{
    public abstract class ViewBase : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool Set<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(storage, value))
            {
                storage = value;
                OnPropertyChanged(propertyName);
                return true;
            }

            return false;
        }

        protected IMessenger Messenger => WeakReferenceMessenger.Default;

        public ViewBase()
        {
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
        }

        protected virtual void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
        }

        protected virtual void OnUnloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Messenger.UnregisterAll(this);
        }

        protected void RunOnUI(Action a)
        {
            this.RunOnDispatcher(a);
        }
    }
}
