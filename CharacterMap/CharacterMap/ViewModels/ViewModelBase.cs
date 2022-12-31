using CharacterMap.Helpers;
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
    public partial class MultiWindowViewModelBase : INotifyPropertyChanged
    {
        private object _lock { get; } = new();
        private Dictionary<SynchronizationContext, PropertyChangedEventHandler> _handlersWithContext { get; } = new();

        public event PropertyChangedEventHandler PropertyChanged
        {
            add
            {
                if (value == null)
                    return;

                var ctx = SynchronizationContext.Current;
                lock (_lock)
                {
                    if (_handlersWithContext.TryGetValue(ctx, out PropertyChangedEventHandler eventHandler))
                    {
                        eventHandler += value;
                        _handlersWithContext[ctx] = eventHandler;
                    }
                    else
                        _handlersWithContext.Add(ctx, value);
                }
            }
            remove
            {
                if (value == null)
                    return;

                var ctx = SynchronizationContext.Current;
                lock (_lock)
                {
                    if (_handlersWithContext.TryGetValue(ctx, out PropertyChangedEventHandler eventHandler))
                    {
                        eventHandler -= value;
                        if (eventHandler != null)
                            _handlersWithContext[ctx] = eventHandler;
                        else
                            _handlersWithContext.Remove(ctx);
                    }
                }
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            KeyValuePair<SynchronizationContext, PropertyChangedEventHandler>[] handlersWithContext;
            lock (_lock)
                handlersWithContext = _handlersWithContext.ToArray();

            PropertyChangedEventArgs eventArgs = new (propertyName);
            foreach (var handlerWithContext in handlersWithContext)
            {
                handlerWithContext.Key.Post(o =>
                {
                    handlerWithContext.Value(this, eventArgs);
                    OnPropertyChangeNotified(propertyName);
                }, null);
            }

            OnPropertyChangeNotified(propertyName);
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
                this.OnPropertyChanged(propertyName);
            return true;
        }

        public bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected virtual void OnPropertyChangeNotified(string propertyName) { }

        public IMessenger Messenger => WeakReferenceMessenger.Default;

        public void Register<T>(Action<T> action, string token = null) where T : class
        {
            if (!string.IsNullOrWhiteSpace(token))
                Messenger.Register<T, string>(this, token, (r, m) => { action(m); });
            else
                Messenger.Register<T>(this, (r, m) => { action(m); });
        }

        public bool AllowAnimation =>
            ResourceHelper.AppSettings.UseSelectionAnimations
            && CompositionFactory.UISettings.AnimationsEnabled;
    }

    public class ViewModelBase : ObservableObject
    {
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
                this.OnPropertyChanged(propertyName);
            return true;
        }

        public bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
            => base.SetProperty(ref field, value, propertyName);

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            OnPropertyChangeNotified(e.PropertyName);
        }

        protected virtual void OnPropertyChangeNotified(string propertyName) { }

        public IMessenger Messenger => WeakReferenceMessenger.Default;

        public void Register<T>(Action<T> action, string token = null) where T : class
        {
            if (!string.IsNullOrWhiteSpace(token))
                Messenger.Register<T, string>(this, token, (r, m) => { action(m); });
            else
                Messenger.Register<T>(this, (r, m) => { action(m); });
        }

        public bool AllowAnimation => 
            ResourceHelper.AppSettings.UseSelectionAnimations 
            && CompositionFactory.UISettings.AnimationsEnabled;
    }
}
