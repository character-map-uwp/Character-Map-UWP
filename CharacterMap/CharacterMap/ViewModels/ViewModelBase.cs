using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Windows.System;

namespace CharacterMap.ViewModels
{
    // Based on https://www.pedrolamas.com/2018/04/19/building-a-multi-window-dispatcher-agnostic-view-model/
    public partial class MultiWindowViewModelBase : BaseNotifyingModel, INotifyPropertyChanged
    {
        private object _lock { get; } = new();

        private Dictionary<SynchronizationContext, PropertyChangedEventHandler> _handlerCache { get; } = new();

        public event PropertyChangedEventHandler PropertyChanged
        {
            add
            {
                if (value == null)
                    return;

                var ctx = SynchronizationContext.Current;
                lock (_lock)
                {
                    if (_handlerCache.TryGetValue(ctx, out PropertyChangedEventHandler eventHandler))
                    {
                        eventHandler += value;
                        _handlerCache[ctx] = eventHandler;
                    }
                    else
                        _handlerCache.Add(ctx, value);
                }
            }
            remove
            {
                if (value == null)
                    return;

                var ctx = SynchronizationContext.Current;
                lock (_lock)
                {
                    if (_handlerCache.TryGetValue(ctx, out PropertyChangedEventHandler eventHandler))
                    {
                        eventHandler -= value;
                        if (eventHandler != null)
                            _handlerCache[ctx] = eventHandler;
                        else
                            _handlerCache.Remove(ctx);
                    }
                }
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            KeyValuePair<SynchronizationContext, PropertyChangedEventHandler>[] handlers;
            lock (_lock)
                handlers = _handlerCache.ToArray();

            PropertyChangedEventArgs eventArgs = new (propertyName);
            foreach (var handler in handlers)
            {
                void Do()
                {
                    handler.Value(this, eventArgs);
                    OnPropertyChangeNotified(propertyName);
                }

                if (SynchronizationContext.Current == handler.Key)
                    Do();
                else
                    handler.Key.Send(o => Do(), null);
            }
        }

        protected override void SendPropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }
    }

    public abstract class BaseNotifyingModel
    {
        SynchronizationContext _originalContext { get; }

        /// <summary>
        /// If true, ViewModel will notify of changes to animation settings
        /// </summary>
        protected virtual bool TrackAnimation => false;

        public BaseNotifyingModel()
        {
            if (TrackAnimation)
            {
                _originalContext = SynchronizationContext.Current;
                Register<AppSettingsChangedMessage>(m =>
                {
                    void Notify(string s) 
                        => OnSyncContext(() => SendPropertyChanged(s));

                    switch (m.PropertyName)
                    {
                        case nameof(AppSettings.UseSelectionAnimations):
                            Notify(nameof(AllowAnimation));
                            Notify(nameof(AllowExpensiveAnimation));
                            Notify(nameof(AllowFluentAnimation));
                            break;
                        case nameof(AppSettings.AllowExpensiveAnimations):
                            Notify(nameof(AllowExpensiveAnimation));
                            break;
                        case nameof(AppSettings.UseFluentPointerOverAnimations):
                            Notify(nameof(AllowFluentAnimation));
                            break;
                    }
                });
            }
        }

        /// <summary>
        /// Runs code on the original <see cref="SynchronizationContext"/> that
        /// the ViewModel was created on. Useful for updating UI dependent bindings.
        /// </summary>
        /// <param name="a"></param>
        protected void OnSyncContext(Action a)
        {
            if (_originalContext is null)
                a();
            else if (SynchronizationContext.Current == _originalContext)
                a();
            else
                _originalContext.Post(_ => a(), null);
        }

        /// <summary>
        /// Private data store that contains all of the properties access through GetProperty 
        /// method.
        /// </summary>
        readonly Dictionary<String, Object> _data = new();

        /// <summary>
        /// Optimised for value types. Gets the value of a property. If the property does not exist, returns the defined default value (and sets that value in the model)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="defaultValue">Default value to set and return if property is null. Sets & returns as default(T) if no value is provided</param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        protected T GetV<T>(T defaultValue = default, [CallerMemberName] String propertyName = null)
        {
            if (_data.TryGetValue(propertyName, out object t))
                return (T)t;

            _data[propertyName] = defaultValue;
            return defaultValue;
        }

        /// <summary>
        /// Optimised for object types
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="defaultValue"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        protected T Get<T>(Func<T> defaultValue = null, [CallerMemberName] String propertyName = null)
        {
            if (_data.TryGetValue(propertyName, out object t))
                return (T)t;

            T value = (defaultValue == null) ? default : defaultValue.Invoke();
            _data[propertyName] = value;
            return value;
        }

        /// <summary>
        /// Attempts to set the value of a property to the internal Key-Value dictionary,
        /// and fires off a PropertyChangedEvent only if the value has changed
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        protected Boolean Set<T>(T value, [CallerMemberName] String propertyName = null, bool notify = true)
        {
            if (_data == null)
                return false;

            if (_data.TryGetValue(propertyName, out object t) && object.Equals(t, value))
                return false;

            _data[propertyName] = value;

            if (notify)
                SendPropertyChanged(propertyName);
            return true;
        }


        public bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            SendPropertyChanged(propertyName);
            return true;
        }

        protected abstract void SendPropertyChanged(string propertyName);

        protected virtual void OnPropertyChangeNotified(string propertyName) { }

        public IMessenger Messenger => WeakReferenceMessenger.Default;

        public void Register<T>(Action<T> action, string token = null) where T : class
        {
            if (!string.IsNullOrWhiteSpace(token))
                Messenger.Register<T, string>(this, token, (r, m) => { action(m); });
            else
                Messenger.Register<T>(this, (r, m) => { action(m); });
        }

        public bool AllowAnimation => ResourceHelper.AllowAnimation;
        public bool AllowExpensiveAnimation => ResourceHelper.AllowExpensiveAnimation;
        public bool AllowFluentAnimation => ResourceHelper.AllowFluentAnimation;
    }

    [ObservableObject]
    public abstract partial class ViewModelBaseInternal : BaseNotifyingModel
    {
    }

    public partial class ViewModelBase : ViewModelBaseInternal
    {
        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            OnPropertyChangeNotified(e.PropertyName);
        }

        protected override void SendPropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }
    }
}
