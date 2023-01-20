// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CharacterMap.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace Microsoft.Toolkit.Uwp.UI.Controls
{
    /// <summary>
    /// In App Notification defines a control to show local notification in the app.
    /// </summary>
    [TemplateVisualState(Name = StateContentVisible, GroupName = GroupContent)]
    [TemplateVisualState(Name = StateContentCollapsed, GroupName = GroupContent)]
    [TemplatePart(Name = DismissButtonPart, Type = typeof(Button))]
    [TemplatePart(Name = ContentPresenterPart, Type = typeof(ContentPresenter))]
    public partial class InAppNotification : ContentControl
    {
        private InAppNotificationDismissKind _lastDismissKind;
        private DispatcherTimer _dismissTimer = new DispatcherTimer();
        private Button _dismissButton;
        private VisualStateGroup _visualStateGroup;
        private ContentPresenter _contentProvider;
        private List<NotificationOptions> _stackedNotificationOptions = new List<NotificationOptions>();

        /// <summary>
        /// Initializes a new instance of the <see cref="InAppNotification"/> class.
        /// </summary>
        public InAppNotification()
        {
            DefaultStyleKey = typeof(InAppNotification);
            _dismissTimer.Tick += DismissTimer_Tick;
        }

        /// <inheritdoc />
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_dismissButton != null)
            {
                _dismissButton.Click -= DismissButton_Click;
            }

            if (_visualStateGroup != null)
            {
                _visualStateGroup.CurrentStateChanging -= OnCurrentStateChanging;
                _visualStateGroup.CurrentStateChanged -= OnCurrentStateChanged;
            }

            _dismissButton = (Button)GetTemplateChild(DismissButtonPart);
            _visualStateGroup = (VisualStateGroup)GetTemplateChild(GroupContent);
            _contentProvider = (ContentPresenter)GetTemplateChild(ContentPresenterPart);

            if (_dismissButton != null)
            {
                _dismissButton.Visibility = ShowDismissButton ? Visibility.Visible : Visibility.Collapsed;
                _dismissButton.Click += DismissButton_Click;
                //AutomationProperties.SetName(_dismissButton, "Dismiss Notification");
            }

            if (_visualStateGroup != null)
            {
                _visualStateGroup.CurrentStateChanging += OnCurrentStateChanging;
                _visualStateGroup.CurrentStateChanged += OnCurrentStateChanged;
            }

            var firstNotification = _stackedNotificationOptions.FirstOrDefault();
            if (firstNotification != null)
            {
                UpdateContent(firstNotification);
                VisualStateManager.GoToState(this, StateContentVisible, ResourceHelper.AllowAnimation);
            }

            AutomationProperties.SetLabeledBy(this, this.GetFirstDescendantOfType<ContentPresenter>());
        }

        /// <summary>
        /// Show notification using the current content.
        /// </summary>
        /// <param name="duration">Displayed duration of the notification in ms (less or equal 0 means infinite duration)</param>
        public void Show(int duration = 0)
        {
            // We keep our current content
            var notificationOptions = new NotificationOptions
            {
                Duration = duration,
                Content = Content
            };

            Show(notificationOptions);
        }

        /// <summary>
        /// Show notification using text as the content of the notification
        /// </summary>
        /// <param name="text">Text used as the content of the notification</param>
        /// <param name="duration">Displayed duration of the notification in ms (less or equal 0 means infinite duration)</param>
        public void Show(string text, int duration = 0)
        {
            var notificationOptions = new NotificationOptions
            {
                Duration = duration,
                Content = text
            };
            Show(notificationOptions);
        }

        /// <summary>
        /// Show notification using UIElement as the content of the notification
        /// </summary>
        /// <param name="element">UIElement used as the content of the notification</param>
        /// <param name="duration">Displayed duration of the notification in ms (less or equal 0 means infinite duration)</param>
        public void Show(UIElement element, int duration = 0)
        {
            var notificationOptions = new NotificationOptions
            {
                Duration = duration,
                Content = element
            };
            Show(notificationOptions);
        }

        /// <summary>
        /// Show notification using <paramref name="dataTemplate"/> as the content of the notification
        /// </summary>
        /// <param name="dataTemplate">DataTemplate used as the content of the notification</param>
        /// <param name="duration">Displayed duration of the notification in ms (less or equal 0 means infinite duration)</param>
        public void Show(DataTemplate dataTemplate, int duration = 0)
        {
            var notificationOptions = new NotificationOptions
            {
                Duration = duration,
                Content = dataTemplate
            };
            Show(notificationOptions);
        }

        /// <summary>
        /// Show notification using <paramref name="content"/> as the content of the notification.
        /// The <paramref name="content"/> will be displayed with the current <see cref="ContentControl.ContentTemplate"/>.
        /// </summary>
        /// <param name="content">The content of the notification</param>
        /// <param name="duration">Displayed duration of the notification in ms (less or equal 0 means infinite duration)</param>
        public void Show(object content, int duration = 0)
        {
            var notificationOptions = new NotificationOptions
            {
                Duration = duration,
                Content = content
            };
            Show(notificationOptions);
        }

        /// <summary>
        /// Dismiss the notification
        /// </summary>
        public void Dismiss()
        {
            Dismiss(InAppNotificationDismissKind.Programmatic);
        }

        /// <summary>
        /// Dismiss the notification
        /// </summary>
        /// <param name="dismissKind">Kind of action that triggered dismiss event</param>
        private void Dismiss(InAppNotificationDismissKind dismissKind)
        {
            if (_stackedNotificationOptions.Count == 0)
            {
                // There is nothing to dismiss.
                return;
            }

            _dismissTimer.Stop();

            // Continue to display notification if on remaining stacked notification
            _stackedNotificationOptions.RemoveAt(0);
            if (_stackedNotificationOptions.Any())
            {
                var notificationOptions = _stackedNotificationOptions[0];

                UpdateContent(notificationOptions);

                if (notificationOptions.Duration > 0)
                {
                    _dismissTimer.Interval = TimeSpan.FromMilliseconds(notificationOptions.Duration);
                    _dismissTimer.Start();
                }

                return;
            }

            var closingEventArgs = new InAppNotificationClosingEventArgs(dismissKind);
            Closing?.Invoke(this, closingEventArgs);

            if (closingEventArgs.Cancel)
            {
                return;
            }

            var result = VisualStateManager.GoToState(this, StateContentCollapsed, ResourceHelper.AllowAnimation);
            if (!result)
            {
                // The state transition cannot be executed.
                // It means that the control's template hasn't been applied or that it doesn't contain the state.
                Visibility = Visibility.Collapsed;
            }

            _lastDismissKind = dismissKind;
        }

        /// <summary>
        /// Update the Content of the notification
        /// </summary>
        /// <param name="notificationOptions">Information about the notification to display</param>
        private void UpdateContent(NotificationOptions notificationOptions)
        {
            if (_contentProvider is null)
            {
                // The control template has not been applied yet.
                return;
            }

            switch (notificationOptions.Content)
            {
                case string text:
                    _contentProvider.ContentTemplate = null;
                    _contentProvider.Content = text;
                    break;
                case UIElement element:
                    _contentProvider.ContentTemplate = null;
                    _contentProvider.Content = element;
                    break;
                case DataTemplate dataTemplate:
                    _contentProvider.ContentTemplate = dataTemplate;
                    _contentProvider.Content = null;
                    break;
                case object content:
                    _contentProvider.ContentTemplate = ContentTemplate;
                    _contentProvider.Content = content;
                    break;
            }

            RaiseAutomationNotification();
        }

        /// <summary>
        /// Handle the display of the notification based on the current StackMode
        /// </summary>
        /// <param name="notificationOptions">Information about the notification to display</param>
        private void Show(NotificationOptions notificationOptions)
        {
            var eventArgs = new InAppNotificationOpeningEventArgs();
            Opening?.Invoke(this, eventArgs);

            if (eventArgs.Cancel)
            {
                return;
            }

            var shouldDisplayImmediately = true;
            switch (StackMode)
            {
                case StackMode.Replace:
                    _stackedNotificationOptions.Clear();
                    _stackedNotificationOptions.Add(notificationOptions);
                    break;
                case StackMode.StackInFront:
                    _stackedNotificationOptions.Insert(0, notificationOptions);
                    break;
                case StackMode.QueueBehind:
                    _stackedNotificationOptions.Add(notificationOptions);
                    shouldDisplayImmediately = _stackedNotificationOptions.Count == 1;
                    break;
                default:
                    break;
            }

            if (shouldDisplayImmediately)
            {
                Visibility = Visibility.Visible;
                VisualStateManager.GoToState(this, StateContentVisible, ResourceHelper.AllowAnimation);

                UpdateContent(notificationOptions);

                if (notificationOptions.Duration > 0)
                {
                    _dismissTimer.Interval = TimeSpan.FromMilliseconds(notificationOptions.Duration);
                    _dismissTimer.Start();
                }
                else
                {
                    _dismissTimer.Stop();
                }
            }
        }

        #region Attached Properties

        /// <summary>
        /// Gets the value of the KeyFrameDuration attached Property
        /// </summary>
        /// <param name="obj">the KeyFrame where the duration is set</param>
        /// <returns>Value of KeyFrameDuration</returns>
        public static TimeSpan GetKeyFrameDuration(DependencyObject obj)
        {
            return (TimeSpan)obj.GetValue(KeyFrameDurationProperty);
        }

        /// <summary>
        /// Sets the value of the KeyFrameDuration attached property
        /// </summary>
        /// <param name="obj">The KeyFrame object where the property is attached</param>
        /// <param name="value">The TimeSpan value to be set as duration</param>
        public static void SetKeyFrameDuration(DependencyObject obj, TimeSpan value)
        {
            obj.SetValue(KeyFrameDurationProperty, value);
        }

        /// <summary>
        /// Using a DependencyProperty as the backing store for KeyFrameDuration. This enables animation, styling, binding, etc
        /// </summary>
        public static readonly DependencyProperty KeyFrameDurationProperty =
            DependencyProperty.RegisterAttached("KeyFrameDuration", typeof(TimeSpan), typeof(InAppNotification), new PropertyMetadata(TimeSpan.Zero, OnKeyFrameAnimationChanged));

        private static void OnKeyFrameAnimationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is TimeSpan ts)
            {
                if (d is DoubleKeyFrame dkf)
                {
                    dkf.KeyTime = KeyTime.FromTimeSpan(ts);
                }
                else if (d is ObjectKeyFrame okf)
                {
                    okf.KeyTime = KeyTime.FromTimeSpan(ts);
                }
            }
        }

        #endregion

        #region Constants

        /// <summary>
        /// Key of the VisualStateGroup that show/dismiss content
        /// </summary>
        private const string GroupContent = "State";

        /// <summary>
        /// Key of the VisualState when content is showed
        /// </summary>
        private const string StateContentVisible = "Visible";

        /// <summary>
        /// Key of the VisualState when content is dismissed
        /// </summary>
        private const string StateContentCollapsed = "Collapsed";

        /// <summary>
        /// Key of the UI Element that dismiss the control
        /// </summary>
        private const string DismissButtonPart = "PART_DismissButton";

        /// <summary>
        /// Key of the UI Element that will display the notification content.
        /// </summary>
        private const string ContentPresenterPart = "PART_Presenter";

        #endregion

        #region Events

        /// <summary>
        /// Event raised when the notification is opening
        /// </summary>
        public event InAppNotificationOpeningEventHandler Opening;

        /// <summary>
        /// Event raised when the notification is opened
        /// </summary>
        public event EventHandler Opened;

        /// <summary>
        /// Event raised when the notification is closing
        /// </summary>
        public event InAppNotificationClosingEventHandler Closing;

        /// <summary>
        /// Event raised when the notification is closed
        /// </summary>
        public event InAppNotificationClosedEventHandler Closed;

        private void DismissButton_Click(object sender, RoutedEventArgs e)
        {
            Dismiss(InAppNotificationDismissKind.User);
        }

        private void DismissTimer_Tick(object sender, object e)
        {
            Dismiss(InAppNotificationDismissKind.Timeout);
        }

        private void OnCurrentStateChanging(object sender, VisualStateChangedEventArgs e)
        {
            if (e.NewState.Name == StateContentVisible)
            {
                Visibility = Visibility.Visible;
            }
        }

        private void OnCurrentStateChanged(object sender, VisualStateChangedEventArgs e)
        {
            switch (e.NewState.Name)
            {
                case StateContentVisible:
                    OnNotificationVisible();
                    break;
                case StateContentCollapsed:
                    OnNotificationCollapsed();
                    break;
            }
        }

        private void OnNotificationVisible()
        {
            Opened?.Invoke(this, EventArgs.Empty);
        }

        private void OnNotificationCollapsed()
        {
            Closed?.Invoke(this, new InAppNotificationClosedEventArgs(_lastDismissKind));
            Visibility = Visibility.Collapsed;
        }

        private void RaiseAutomationNotification()
        {
            if (!AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
            {
                return;
            }

            var peer = FrameworkElementAutomationPeer.CreatePeerForElement(this);
            peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Identifies the <see cref="ShowDismissButton"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty ShowDismissButtonProperty =
            DependencyProperty.Register(nameof(ShowDismissButton), typeof(bool), typeof(InAppNotification), new PropertyMetadata(true, OnShowDismissButtonChanged));

        /// <summary>
        /// Identifies the <see cref="AnimationDuration"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty AnimationDurationProperty =
            DependencyProperty.Register(nameof(AnimationDuration), typeof(TimeSpan), typeof(InAppNotification), new PropertyMetadata(TimeSpan.FromMilliseconds(100)));

        /// <summary>
        /// Identifies the <see cref="VerticalOffset"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty VerticalOffsetProperty =
            DependencyProperty.Register(nameof(VerticalOffset), typeof(double), typeof(InAppNotification), new PropertyMetadata(100));

        /// <summary>
        /// Identifies the <see cref="HorizontalOffset"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty HorizontalOffsetProperty =
            DependencyProperty.Register(nameof(HorizontalOffset), typeof(double), typeof(InAppNotification), new PropertyMetadata(0));

        /// <summary>
        /// Identifies the <see cref="StackMode"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty StackModeProperty =
            DependencyProperty.Register(nameof(StackMode), typeof(StackMode), typeof(InAppNotification), new PropertyMetadata(StackMode.Replace));

        /// <summary>
        /// Gets or sets a value indicating whether to show the Dismiss button of the control.
        /// </summary>
        public bool ShowDismissButton
        {
            get { return (bool)GetValue(ShowDismissButtonProperty); }
            set { SetValue(ShowDismissButtonProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating the duration of the popup animation (in milliseconds).
        /// </summary>
        public TimeSpan AnimationDuration
        {
            get { return (TimeSpan)GetValue(AnimationDurationProperty); }
            set { SetValue(AnimationDurationProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating the vertical offset of the popup animation.
        /// </summary>
        public double VerticalOffset
        {
            get { return (double)GetValue(VerticalOffsetProperty); }
            set { SetValue(VerticalOffsetProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating the horizontal offset of the popup animation.
        /// </summary>
        public double HorizontalOffset
        {
            get { return (double)GetValue(HorizontalOffsetProperty); }
            set { SetValue(HorizontalOffsetProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating the stack mode of the notifications.
        /// </summary>
        public StackMode StackMode
        {
            get { return (StackMode)GetValue(StackModeProperty); }
            set { SetValue(StackModeProperty, value); }
        }

        private static void OnShowDismissButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var inApNotification = d as InAppNotification;

            if (inApNotification._dismissButton != null)
            {
                bool showDismissButton = (bool)e.NewValue;
                inApNotification._dismissButton.Visibility = showDismissButton ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        #endregion
    }

    /// <summary>
    /// A delegate for <see cref="InAppNotification"/> dismissing.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event arguments.</param>
    public delegate void InAppNotificationClosedEventHandler(object sender, InAppNotificationClosedEventArgs e);

    /// <summary>
    /// Provides data for the <see cref="InAppNotification"/> Dismissing event.
    /// </summary>
    public class InAppNotificationClosedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InAppNotificationClosedEventArgs"/> class.
        /// </summary>
        /// <param name="dismissKind">Dismiss kind that triggered the closing event</param>
        public InAppNotificationClosedEventArgs(InAppNotificationDismissKind dismissKind)
        {
            DismissKind = dismissKind;
        }

        /// <summary>
        /// Gets the kind of action for the closing event.
        /// </summary>
        public InAppNotificationDismissKind DismissKind { get; private set; }
    }

    /// <summary>
    /// A delegate for <see cref="InAppNotification"/> dismissing.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event arguments.</param>
    public delegate void InAppNotificationClosingEventHandler(object sender, InAppNotificationClosingEventArgs e);

    /// <summary>
    /// Provides data for the <see cref="InAppNotification"/> Dismissing event.
    /// </summary>
    public class InAppNotificationClosingEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InAppNotificationClosingEventArgs"/> class.
        /// </summary>
        /// <param name="dismissKind">Dismiss kind that triggered the closing event</param>
        public InAppNotificationClosingEventArgs(InAppNotificationDismissKind dismissKind)
        {
            DismissKind = dismissKind;
        }

        /// <summary>
        /// Gets the kind of action for the closing event.
        /// </summary>
        public InAppNotificationDismissKind DismissKind { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether the notification should be closed.
        /// </summary>
        public bool Cancel { get; set; }
    }

    /// <summary>
    /// Enumeration to describe how an InAppNotification was dismissed
    /// </summary>
    public enum InAppNotificationDismissKind
    {
        /// <summary>
        /// When the system dismissed the notification.
        /// </summary>
        Programmatic,

        /// <summary>
        /// When user explicitly dismissed the notification.
        /// </summary>
        User,

        /// <summary>
        /// When the system dismissed the notification after timeout.
        /// </summary>
        Timeout
    }

    /// <summary>
    /// A delegate for <see cref="InAppNotification"/> opening.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event arguments.</param>
    public delegate void InAppNotificationOpeningEventHandler(object sender, InAppNotificationOpeningEventArgs e);

    /// <summary>
    /// Provides data for the <see cref="InAppNotification"/> Dismissing event.
    /// </summary>
    public class InAppNotificationOpeningEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InAppNotificationOpeningEventArgs"/> class.
        /// </summary>
        public InAppNotificationOpeningEventArgs()
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether the notification should be opened.
        /// </summary>
        public bool Cancel { get; set; }
    }

    /// <summary>
    /// Base class that contains options of notification
    /// </summary>
    internal class NotificationOptions
    {
        /// <summary>
        /// Gets or sets duration of the stacked notification
        /// </summary>
        public int Duration { get; set; }

        /// <summary>
        /// Gets or sets Content of the notification
        /// Could be either a <see cref="string"/> or a <see cref="UIElement"/> or a <see cref="DataTemplate"/>
        /// </summary>
        public object Content { get; set; }
    }

    /// <summary>
    /// The Stack mode of an in-app notification.
    /// </summary>
    public enum StackMode
    {
        /// <summary>
        /// Each notification will replace the previous one
        /// </summary>
        Replace,

        /// <summary>
        /// Opening a notification will display it immediately, remaining notifications will appear when a notification is dismissed
        /// </summary>
        StackInFront,

        /// <summary>
        /// Dismissing a notification will show the next one in the queue
        /// </summary>
        QueueBehind
    }
}