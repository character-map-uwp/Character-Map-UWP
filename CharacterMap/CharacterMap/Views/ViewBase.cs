using CharacterMap.Annotations;
using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Views
{
    [ObservableObject]
    public abstract partial class ViewBase : Page
    {
        protected IMessenger Messenger => WeakReferenceMessenger.Default;

        /// <summary>
        /// Returns true if the process is running within the VS designer
        /// </summary>
        protected bool DesignMode => Windows.ApplicationModel.DesignMode.DesignModeEnabled;

        public ViewBase()
        {
            this.Loaded += OnLoadedBase;
            this.Unloaded += OnUnloaded;

            ResourceHelper.GoToThemeState(this);

            if (DesignMode)
                return;

            LeakTrackingService.Register(this);
        }

        private void OnLoadedBase(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            ResourceHelper.GoToThemeState(this);
            OnLoaded(sender, e);
        }

        protected virtual void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
        }

        protected virtual void OnUnloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Messenger.UnregisterAll(this);
        }

        protected void Register<T>(Action<T> handler) where T : class
        {
            Messenger.Register<T>(this, (o, m) => handler(m));
        }

        protected void Unregister<T>() where T : class
        {
            Messenger.Unregister<T>(this);
        }

        protected void RunOnUI(Action a)
        {
            this.RunOnDispatcher(a);
        }
    }
}
