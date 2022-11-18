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
using Windows.ApplicationModel;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Views
{
    [ObservableObject]
    public abstract partial class ViewBase : Page
    {
        protected IMessenger Messenger => WeakReferenceMessenger.Default;

        public ViewBase()
        {
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;

            RequestedTheme = ResourceHelper.GetEffectiveTheme();
            ResourceHelper.GoToThemeState(this);

            if (DesignMode.DesignModeEnabled)
                return;

            LeakTrackingService.Register(this);
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
