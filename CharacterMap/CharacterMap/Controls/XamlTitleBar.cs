using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using System;
using Windows.ApplicationModel.Core;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls
{
    public partial class XamlTitleBarTemplateSettings : ViewModelBase
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

        private GridLength _gridHeight = new GridLength(0);
        public GridLength GridHeight
        {
            get => _gridHeight;
            internal set => Set(ref _gridHeight, value);
        }

        private double _height = 0d;
        public double Height
        {
            get => _height;
            internal set => Set(ref _height, value);
        }
    }

    public sealed class XamlTitleBar : ContentControl
    {
        private UISettings _settings;
        private CoreApplicationViewTitleBar _titleBar;
        private CoreWindow _window;
        private FrameworkElement _backgroundElement;

        public bool IsDragTarget
        {
            get { return (bool)GetValue(IsDragTargetProperty); }
            set { SetValue(IsDragTargetProperty, value); }
        }

        public static readonly DependencyProperty IsDragTargetProperty =
            DependencyProperty.Register(nameof(IsDragTarget), typeof(bool), typeof(XamlTitleBar), new PropertyMetadata(true, (d,e) =>
            {
                if (d is XamlTitleBar bar)
                    bar.UpdateStates();
            }));


        public bool IsAutoHeightEnabled
        {
            get { return (bool)GetValue(IsAutoHeightEnabledProperty); }
            set { SetValue(IsAutoHeightEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsAutoHeightEnabledProperty =
            DependencyProperty.Register(nameof(IsAutoHeightEnabled), typeof(bool), typeof(XamlTitleBar), new PropertyMetadata(true));


        public bool AutoUpdateTitle
        {
            get { return (bool)GetValue(AutoUpdateTitleProperty); }
            set { SetValue(AutoUpdateTitleProperty, value); }
        }

        public static readonly DependencyProperty AutoUpdateTitleProperty =
            DependencyProperty.Register(nameof(AutoUpdateTitle), typeof(bool), typeof(XamlTitleBar), new PropertyMetadata(true));




        public XamlTitleBarTemplateSettings TemplateSettings { get; } = new XamlTitleBarTemplateSettings();

        public XamlTitleBar()
        {
            DefaultStyleKey = typeof(XamlTitleBar);
            Loading += XamlTitleBar_Loading;
            Loaded += XamlTitleBar_Loaded;
            Unloaded += XamlTitleBar_Unloaded;
        }

        private void XamlTitleBar_Loading(FrameworkElement sender, object args)
        {
            // 32px is the default height at default text scaling. 
            // We'll use this as a placeholder until we get a real value
            SetHeight(32);
        }

        void SetHeight(double d)
        {
            d = Math.Max(d, MinHeight);

            if (IsAutoHeightEnabled)
                Height = d;
            
            TemplateSettings.GridHeight = new GridLength(d);
            TemplateSettings.Height = d;
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (GetTemplateChild("BackgroundElement") is FrameworkElement e)
            {
                _backgroundElement = e;
                UpdateDragElement();
                TryUpdateMetrics();
            }
        }

        public void TryUpdateMetrics()
        {
            try
            {
                // Attempts to avoid title bar not showing immediately when a window is opened
                _titleBar = CoreApplication.GetCurrentView().TitleBar;
                _titleBar.ExtendViewIntoTitleBar = true;
                UpdateMetrics(_titleBar);
                UpdateColors();
            }
            catch
            { }
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

            TemplateSettings.Messenger.Register<AppSettingsChangedMessage>(this, (o, m) => OnAppSettingsChanged(m));
            TemplateSettings.Messenger.Register<string, string>(this, "TitleUpdated", (o, m) =>
            {
                if (Dispatcher.HasThreadAccess)
                    UpdateTitle();
            });

            UpdateTitle();
            UpdateColors();
            UpdateMetrics(_titleBar);
        }

        void UpdateTitle()
        {
            if (AutoUpdateTitle && this.GetTemplateChild("TitleTextLabel") is TextBlock t)
            {
                string text = TitleBarHelper.GetTitle();
                if (string.IsNullOrWhiteSpace(text))
                    text = ResourceHelper.GetAppName();
                else
                    text += $" - {ResourceHelper.GetAppName()}";

                t.Text = text;
            }
        }

        private void OnAppSettingsChanged(AppSettingsChangedMessage obj)
        {
            if (obj.PropertyName == nameof(Core.AppSettings.UserRequestedTheme))
            {
                _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, UpdateColors);
            }
        }

        private void UnhookListeners()
        {
            TemplateSettings.Messenger.UnregisterAll(this);

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

            TemplateSettings.Messenger.UnregisterAll(this);


            _window = null;
        }

        public FrameworkElement GetDragElement() => _backgroundElement;

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

        private static ApplicationView TryGetCurrentView()
        {
            ApplicationView view = null;
            try
            {
                view = ApplicationView.GetForCurrentView();
            }
            catch { }
            return view;
        }

        private void UpdateColors()
        {
            if (_settings == null)
                return;

            bool active = _window != null && _window.ActivationMode == CoreWindowActivationMode.ActivatedInForeground;

            TemplateSettings.BackgroundColor = Colors.Transparent;// _settings.GetColorValue(active ? UIColorType.Accent : UIColorType.AccentDark1);

            var darkAccent = _settings.GetColorValue(UIColorType.AccentDark1);
            var btnHoverColor = _settings.GetColorValue(UIColorType.AccentLight1);
            var foreground = ResourceHelper.GetEffectiveTheme() == ElementTheme.Dark ? Colors.White : Colors.Black;

            // TODO: HACK - make proper
            if (ResourceHelper.AppSettings.ApplicationDesignTheme == (int)DesignStyle.ClassicWindows)
                foreground = Colors.White;

            ApplyColorToTitleBar(
                Colors.Transparent,
                foreground,
                darkAccent,
                Colors.Gray);

            ApplyColorToTitleButton(
                Colors.Transparent, foreground,
                btnHoverColor, foreground,
                Colors.Transparent, foreground,
                Colors.Transparent, Colors.Gray);
        }

        private static void ApplyColorToTitleBar(Color? titleBackgroundColor,
            Color? titleForegroundColor,
            Color? titleInactiveBackgroundColor,
            Color? titleInactiveForegroundColor)
        {
            var view = TryGetCurrentView();
            if (view == null)
                return;

            // active
            view.TitleBar.BackgroundColor = titleBackgroundColor;
            view.TitleBar.ForegroundColor = titleForegroundColor;

            // inactive
            view.TitleBar.InactiveBackgroundColor = titleInactiveBackgroundColor;
            view.TitleBar.InactiveForegroundColor = titleInactiveForegroundColor;
        }

        private static void ApplyColorToTitleButton(Color? titleButtonBackgroundColor,
            Color? titleButtonForegroundColor,
            Color? titleButtonHoverBackgroundColor,
            Color? titleButtonHoverForegroundColor,
            Color? titleButtonPressedBackgroundColor,
            Color? titleButtonPressedForegroundColor,
            Color? titleButtonInactiveBackgroundColor,
            Color? titleButtonInactiveForegroundColor)
        {
            var view = TryGetCurrentView();
            if (view == null)
                return;

            // button
            view.TitleBar.ButtonBackgroundColor = titleButtonBackgroundColor;
            view.TitleBar.ButtonForegroundColor = titleButtonForegroundColor;

            view.TitleBar.ButtonHoverBackgroundColor = titleButtonHoverBackgroundColor;
            view.TitleBar.ButtonHoverForegroundColor = titleButtonHoverForegroundColor;

            view.TitleBar.ButtonPressedBackgroundColor = titleButtonPressedBackgroundColor;
            view.TitleBar.ButtonPressedForegroundColor = titleButtonPressedForegroundColor;

            view.TitleBar.ButtonInactiveBackgroundColor = titleButtonInactiveBackgroundColor;
            view.TitleBar.ButtonInactiveForegroundColor = titleButtonInactiveForegroundColor;
        }

        private void UpdateMetrics(CoreApplicationViewTitleBar bar)
        {
            bool ltr = FlowDirection == FlowDirection.LeftToRight;

            if (bar.Height > 0)
                SetHeight(bar.Height);

            TemplateSettings.LeftColumnWidth 
                = new GridLength(ltr ? bar.SystemOverlayLeftInset : bar.SystemOverlayRightInset);

            TemplateSettings.RightColumnWidth 
                = new GridLength(ltr ? bar.SystemOverlayRightInset : bar.SystemOverlayLeftInset);
        }


        private void UpdateStates()
        {
            if (IsDragTarget)
                VisualStateManager.GoToState(this, "ActiveState", true);
            else
                VisualStateManager.GoToState(this, "InactiveState", true);
        }

        private void UpdateDragElement()
        {
            UpdateStates();   

            if (IsDragTarget && _backgroundElement != null)
            {
                TitleBarHelper.SetTitleBar(this);
            }
        }

        private bool IsAccentColorDark()
        {
            var c = _settings.GetColorValue(UIColorType.Accent);
            var isDark = (5 * c.G + 2 * c.R + c.B) <= 8 * 128;
            return isDark;
        }
    }
}
