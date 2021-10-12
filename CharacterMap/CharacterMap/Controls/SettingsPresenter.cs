using CharacterMap.Core;
using CharacterMap.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls
{
    public enum ContentPlacement
    {
        Right = 0,
        Bottom = 1
    }


    [ContentProperty(Name = nameof(Content))]
    public sealed class SettingsPresenter : Control, IThemeableControl
    {
        #region Dependency Properties 

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(SettingsPresenter), new PropertyMetadata(null));


        public string Description
        {
            get { return (string)GetValue(DescriptionProperty); }
            set { SetValue(DescriptionProperty, value); }
        }

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingsPresenter), new PropertyMetadata(null, (d,e) =>
            {
                ((SettingsPresenter)d).UpdateDescriptionStates();
            }));


        public object Content
        {
            get { return (object)GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }

        public static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register(nameof(Content), typeof(object), typeof(SettingsPresenter), new PropertyMetadata(null));


        public object Icon
        {
            get { return (object)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(object), typeof(SettingsPresenter), new PropertyMetadata(null, (d,e) =>
            {
                ((SettingsPresenter)d).UpdateIconStates();
            }));


        public double IconSize
        {
            get { return (double)GetValue(IconSizeProperty); }
            set { SetValue(IconSizeProperty, value); }
        }

        public static readonly DependencyProperty IconSizeProperty =
            DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(SettingsPresenter), new PropertyMetadata(24d));

        public ContentPlacement ContentPlacement
        {
            get { return (ContentPlacement)GetValue(ContentPlacementProperty); }
            set { SetValue(ContentPlacementProperty, value); }
        }

        public static readonly DependencyProperty ContentPlacementProperty =
            DependencyProperty.Register(nameof(ContentPlacement), typeof(ContentPlacement), typeof(SettingsPresenter), new PropertyMetadata(ContentPlacement.Right, (d,e) =>
            {
                ((SettingsPresenter)d).UpdatePlacementStates();
            }));

        #endregion

        private ThemeHelper _themer;

        public SettingsPresenter()
        {
            Properties.SetStyleKey(this, "DefaultSettingsPresenterStyle");
            this.DefaultStyleKey = typeof(SettingsPresenter);
            _themer = new ThemeHelper(this);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            UpdatePlacementStates();
            UpdateIconStates();
            UpdateDescriptionStates();
            //_themer.Update();
        }

        void UpdatePlacementStates()
        {
            VisualStateManager.GoToState(this, $"{ContentPlacement}PlacementState", true);
        }

        void UpdateIconStates()
        {
            string state = Icon is null ? "NoIconState" : "IconState";
            VisualStateManager.GoToState(this, state, true);
        }

        private void UpdateDescriptionStates()
        {
            string state = string.IsNullOrWhiteSpace(Description) ? "NoDescriptionState" : "DescriptionState";
            VisualStateManager.GoToState(this, state, true);
        }

        public void UpdateTheme()
        {
            ResourceHelper.TryResolveThemeStyle3(this);
        }
    }
}
