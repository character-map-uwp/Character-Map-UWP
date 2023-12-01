using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

public abstract class ControlBase : Control
{
    public static PropertyMetadata NullMetadata { get; } = new(null);

    public delegate void DPChangedCallback<V, T>(T source, V oldValue, V newValue);

    int _loadCount = 0;

    public bool IsActualLoaded
    {
        get => (bool)GetValue(IsActualLoadedProperty);
        private set => SetValue(IsActualLoadedProperty, value);
    }

    public static readonly DP IsActualLoadedProperty = DP<bool, ControlBase>(false);

    public ControlBase()
    {
        this.Loaded += ControlBase_Loaded;
        this.Unloaded += ControlBase_Unloaded;
    }

    #region Loaded

    private void ControlBase_Loaded(object sender, RoutedEventArgs e)
    {
        _loadCount++;
        if (_loadCount == 1)
            OnLoaded();
    }

    private void ControlBase_Unloaded(object sender, RoutedEventArgs e)
    {
        _loadCount++;
        if (_loadCount == 0)
            OnUnloaded();
    }

    protected virtual void OnLoaded() { }

    protected virtual void OnUnloaded() { }


    #endregion

    #region DP
    public static DependencyProperty DP<V, T>(V defaultValue, DPChangedCallback<V, T> callback = null, [CallerMemberName] string name = null) where T : DependencyObject
    {
        PropertyChangedCallback c = null;
        if (callback != null)
        {
            c = (d, e) =>
            {
                callback.Invoke(
                    (T)d,
                    e.OldValue is V oldV ? oldV : default,
                    e.NewValue is V newV ? newV : default);
            };
        }

        return DependencyProperty.Register(name.Remove(name.Length - 8), typeof(V), typeof(T), new PropertyMetadata(defaultValue, c));
    }

    public static DependencyProperty DP<V, T>(V defaultValue, Action<T> callback, [CallerMemberName] string name = null) where T : DependencyObject
    {
        PropertyChangedCallback c = null;
        if (callback != null)
        {
            c = (d, e) =>
            {
                callback.Invoke((T)d);
            };
        }

        return DependencyProperty.Register(name.Remove(name.Length - 8), typeof(V), typeof(T), new PropertyMetadata(defaultValue, c));
    }

    public static DependencyProperty DP<V, T>(V defaultValue, PropertyChangedCallback callback, [CallerMemberName] string name = null) where T : DependencyObject
    {
        return DependencyProperty.Register(name.Remove(name.Length - 8), typeof(V), typeof(T), new PropertyMetadata(defaultValue, callback));
    }

    public static DependencyProperty DP<V, Y>(PropertyMetadata metadata = null, [CallerMemberName] string name = null)
    {
        return DependencyProperty.Register(name.Remove(name.Length - 8), typeof(V), typeof(Y), metadata ?? NullMetadata);
    }



    public static DependencyProperty DP<V, Y>(PropertyChangedCallback callback, [CallerMemberName] string name = null)
    {
        return DependencyProperty.Register(name.Remove(name.Length - 8), typeof(V), typeof(Y), new PropertyMetadata(null, callback));
    }

    #endregion

    #region AP

    /// <summary>
    /// Registers an AttachedProperty
    /// </summary>
    /// <typeparam name="V">Property Type</typeparam>
    /// <typeparam name="Y">Owner Type</typeparam>
    /// <param name="metadata"></param>
    /// <param name="name">Property Name. Leave blank.</param>
    /// <returns></returns>
    public static DependencyProperty AP<V, Y>(PropertyMetadata metadata = null, [CallerMemberName] string name = null)
    {
        return DependencyProperty.RegisterAttached(name.Remove(name.Length - 8), typeof(V), typeof(Y), metadata ?? NullMetadata);
    }

    public static DP AP<V, Y>(V defaultValue, PropertyChangedCallback callback, [CallerMemberName] string name = null)
    {
        return DependencyProperty.RegisterAttached(name.Remove(name.Length - 8), typeof(V), typeof(Y), new PropertyMetadata(defaultValue, callback));
    }

    #endregion
}

public abstract class ControlBase<T> : ControlBase where T : DependencyObject
{
    protected static DP DP<V>(V defaultValue, Action<T> callback, [CallerMemberName] string name = null)
    {
        return DP<V, T>(defaultValue, callback, name);
    }

    protected static DP DP<V>(V defaultValue, DPChangedCallback<V, T> callback = null, [CallerMemberName] string name = null)
    {
        return DP<V, T>(defaultValue, callback, name);
    }

    protected static DP DP<V>(PropertyMetadata metadata = null, [CallerMemberName] string name = null)
    {
        return DP<V, T>(metadata, name);
    }

    public static DependencyProperty AP<V>(V defaultValue, [CallerMemberName] string name = null)
    {
        return AP<V, T>(new PropertyMetadata(defaultValue), name);
    }

    public static DependencyProperty AP<V>(PropertyMetadata metadata = null, [CallerMemberName] string name = null)
    {
        return AP<V, T>(metadata, name);
    }

    public static DP AP<V>(V defaultValue, PropertyChangedCallback callback, [CallerMemberName] string name = null)
    {
        return AP<V, T>(defaultValue, callback, name);
    }
}
