using CharacterMap.Helpers;
using GalaSoft.MvvmLight;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Core;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls
{
    public class XamlTitleBarTemplateSettings : ViewModelBase
    {
        private GridLength _leftColumnWidth = new GridLength(0);
        public GridLength LeftColumnWidth
        {
            get => _leftColumnWidth;
            internal set => Set(ref _leftColumnWidth, value);
        }

        private GridLength _rightColumnWidth = new GridLength(0);
        public GridLength RightColumnWidth
        {
            get => _rightColumnWidth;
            internal set => Set(ref _rightColumnWidth, value);
        }

        private Color _backgroundColor = Colors.Transparent;
        public Color BackgroundColor
        {
            get => _backgroundColor;
            internal set => Set(ref _backgroundColor, value);
        }
    }

    public sealed class XamlTitleBar : ContentControl
    {
        private UISettings _settings = null;
        private CoreApplicationViewTitleBar _titleBar = null;
        private CoreWindow _window = null;
        private FrameworkElement _backgroundElement = null;

        public XamlTitleBarTemplateSettings TemplateSettings { get; } = new XamlTitleBarTemplateSettings();

        public XamlTitleBar()
        {
            this.DefaultStyleKey = typeof(XamlTitleBar);
            this.Loaded += XamlTitleBar_Loaded;
            this.Unloaded += XamlTitleBar_Unloaded;
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (this.GetTemplateChild("BackgroundElement") is FrameworkElement e)
            {
                _backgroundElement = e;
                UpdateDragElement();
            }
        }

        private void XamlTitleBar_Loaded(object sender, RoutedEventArgs e)
        {
            HookListeners();
            UpdateDragElement();
            _titleBar.ExtendViewIntoTitleBar = true;
        }

        private void XamlTitleBar_Unloaded(object sender, RoutedEventArgs e)
        {
            UnhookListeners();
        }

        private void HookListeners()
        {
            UnhookListeners();

            CoreApplicationView view = CoreApplication.GetCurrentView();

            _titleBar = view.TitleBar;
            _titleBar.LayoutMetricsChanged += TitleBar_LayoutMetricsChanged;

            _window = view.CoreWindow;
            _window.Activated += _window_Activated;

            _settings = new UISettings();
            _settings.ColorValuesChanged += _settings_ColorValuesChanged;

            UpdateColors();
            UpdateMetrics(_titleBar);
        }

        private void UnhookListeners()
        {
            if (_settings != null)
            {
                _settings.ColorValuesChanged -= _settings_ColorValuesChanged;
            }
            _settings = null;

            if (_titleBar != null)
            {
                _titleBar.LayoutMetricsChanged -= TitleBar_LayoutMetricsChanged;
            }
            _titleBar = null;

            if (_window != null)
            {
                _window.Activated -= _window_Activated;
            }
            _window = null;
        }

        private void _settings_ColorValuesChanged(UISettings sender, object args)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, UpdateColors);
        }

        private void _window_Activated(CoreWindow sender, WindowActivatedEventArgs args)
        {
            UpdateColors();
        }

        private void TitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            UpdateMetrics(sender);
        }

        private void UpdateColors()
        {
            bool active = _window.ActivationMode == CoreWindowActivationMode.ActivatedInForeground;

            if (active)
            {
                TemplateSettings.BackgroundColor = _settings.GetColorValue(UIColorType.Accent);
            }
            else
            {
                TemplateSettings.BackgroundColor = _settings.GetColorValue(UIColorType.AccentDark1);
            }

            var accentColor = _settings.GetColorValue(UIColorType.Accent);
            var darkAccent = _settings.GetColorValue(UIColorType.AccentDark1);
            var btnHoverColor = _settings.GetColorValue(UIColorType.AccentLight1);

            Edi.UWP.Helpers.UI.ApplyColorToTitleBar(
                accentColor,
                Colors.White,
                darkAccent,
                Colors.Gray);

            Edi.UWP.Helpers.UI.ApplyColorToTitleButton(
                accentColor, Colors.White,
                btnHoverColor, Colors.White,
                accentColor, Colors.White,
                Colors.Transparent, Colors.Gray);
        }

        private void UpdateMetrics(CoreApplicationViewTitleBar bar)
        {
            bool ltr = FlowDirection == FlowDirection.LeftToRight;

            this.Height = bar.Height;

            this.TemplateSettings.LeftColumnWidth 
                = new GridLength(ltr ? bar.SystemOverlayLeftInset : bar.SystemOverlayRightInset);

            this.TemplateSettings.RightColumnWidth 
                = new GridLength(ltr ? bar.SystemOverlayRightInset : bar.SystemOverlayLeftInset);
        }

        private void UpdateDragElement()
        {
            if (_backgroundElement != null)
            {
                Window.Current.SetTitleBar(_backgroundElement);
            }
        }
    }
}
