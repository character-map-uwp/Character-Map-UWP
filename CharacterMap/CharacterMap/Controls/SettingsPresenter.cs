using CharacterMap.Core;
using CharacterMap.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Xml.Linq;
using Windows.UI.Composition;
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
    public sealed class SettingsPresenter : ItemsControl, IThemeableControl
    {
        #region Dependency Properties 

        public object Title
        {
            get { return (object)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(object), typeof(SettingsPresenter), new PropertyMetadata(null));


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


        public bool HasItems
        {
            get { return (bool)GetValue(HasItemsProperty); }
            private set { SetValue(HasItemsProperty, value); }
        }

        public static readonly DependencyProperty HasItemsProperty =
            DependencyProperty.Register(nameof(HasItems), typeof(bool), typeof(SettingsPresenter), new PropertyMetadata(false));


        public CornerRadius BottomCornerRadius
        {
            get { return (CornerRadius)GetValue(BottomCornerRadiusProperty); }
            set { SetValue(BottomCornerRadiusProperty, value); }
        }

        public static readonly DependencyProperty BottomCornerRadiusProperty =
            DependencyProperty.Register(nameof(BottomCornerRadius), typeof(CornerRadius), typeof(SettingsPresenter), new PropertyMetadata(new CornerRadius()));

        #endregion

        private ThemeHelper _themer;

        private FrameworkElement _itemsRoot = null;

        public SettingsPresenter()
        {
            Properties.SetStyleKey(this, "DefaultSettingsPresenterStyle");
            this.DefaultStyleKey = typeof(SettingsPresenter);
            _themer = new ThemeHelper(this);
        }

        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return false;
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            UpdateCornerRadius();
        }

        protected override void OnItemsChanged(object e)
        {
            if (e is not null)
                base.OnItemsChanged(e);

            HasItems = Items.Count > 0;

            if (HasItems)
            {
                if (_itemsRoot is null)
                {
                    // force x:Load on ItemsPresenter
                    _itemsRoot = this.GetTemplateChild("ItemsRoot") as FrameworkElement;
                }

                VisualStateManager.GoToState(this, "HasItemsState", ResourceHelper.AllowAnimation);
                if (_itemsRoot is not null
                    && e is not null
                    && ResourceHelper.AllowAnimation
                    && VisualTreeHelperExtensions.GetImplementationRoot(_itemsRoot) is FrameworkElement target)
                {
                    Visual v = target.EnableCompositionTranslation().GetElementVisual();
                    var ease = v.Compositor.GetCachedEntranceEase();
                    v.StartAnimation(
                        v.CreateVector3KeyFrameAnimation(CompositionFactory.TRANSLATION)
                            .AddKeyFrame(0, "Vector3(0, -this.Target.Size.Y - 8, 0)")
                            .AddKeyFrame(1, new Vector3(), ease)
                            .SetDelay(0, AnimationDelayBehavior.SetInitialValueBeforeDelay)
                            .SetDuration(0.4));
                }

                UpdateCornerRadius();
            }
            else
                VisualStateManager.GoToState(this, "NoItemsState", ResourceHelper.AllowAnimation);
        }

        private void UpdateCornerRadius()
        {
            if (this.ItemsPanelRoot is null)
                return;

            foreach (var item in this.ItemsPanelRoot.Children)
            {
                if (item is ContentPresenter f)
                {
                    if (f.Content == this.Items.Last())
                        f.CornerRadius = this.BottomCornerRadius;
                    else
                        f.CornerRadius = new CornerRadius();
                }
            }
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            UpdatePlacementStates();
            UpdateIconStates();
            UpdateDescriptionStates();

            OnItemsChanged(null);
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
