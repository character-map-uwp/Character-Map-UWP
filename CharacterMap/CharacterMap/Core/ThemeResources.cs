using CharacterMap.Helpers;
using CharacterMap.Models;
using CommunityToolkit.Mvvm.Messaging;
using System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Core
{
    [Bindable]
    internal class ThemeResources
    {
        public event EventHandler ThemeChanged;

        private static ThemeResources _default { get; } = new();

        private UISettings _settings { get; }

        private ThemeResources()
        {
            _settings = new();
            _settings.ColorValuesChanged += _settings_ColorValuesChanged;

            static void _settings_ColorValuesChanged(UISettings sender, object args)
            {
                _default.ThemeChanged.Invoke(null, EventArgs.Empty);
            }

            WeakReferenceMessenger.Default.Register<AppSettingsChangedMessage>(_settings, (o, m) =>
            {
                if (m.PropertyName == nameof(AppSettings.UserRequestedTheme))
                {
                    _default.ThemeChanged.Invoke(null, EventArgs.Empty);
                }
            });
        }




        //------------------------------------------------------
        //
        // Theme Resource Properties
        //
        //------------------------------------------------------

        public static string GetBrushColor(DependencyObject obj)
        {
            return (string)obj.GetValue(BrushColor);
        }

        public static void SetBrushColor(DependencyObject obj, string value)
        {
            obj.SetValue(BrushColor, value);
        }

        public static readonly DependencyProperty BrushColor =
            DependencyProperty.RegisterAttached("BrushColor", typeof(string), typeof(ThemeResources), new PropertyMetadata(null, (d, a) =>
            {
                if (d is DependencyObject obj)
                    Track(obj, () =>
                    {
                        if (ResourceHelper.TryGet<Color>(GetBrushColor(obj), out Color value)
                            && d is SolidColorBrush b)
                                b.Color = value;
                    });
            }));




        //------------------------------------------------------
        //
        // Scaffolding
        //
        //------------------------------------------------------

        static bool Run(DependencyObject obj, DispatchedHandler d)
        {
            try
            {
                if (obj.Dispatcher.HasThreadAccess)
                    d();
                else
                    _ = obj.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, d);
                return true;
            }
            catch (System.Runtime.InteropServices.InvalidComObjectException)
            {
                // object is no longer alive
                return false;
            }
        }

        static void Track(DependencyObject obj, DispatchedHandler h)
        {
            _default.ThemeChanged -= Changed;
            _default.ThemeChanged += Changed;
            Evaluate();

            void Changed(object sender, EventArgs e) => Evaluate();
            void Evaluate()
            {
                if (!Run(obj, h))
                    _default.ThemeChanged -= Changed;
            }
        }
    }
}
