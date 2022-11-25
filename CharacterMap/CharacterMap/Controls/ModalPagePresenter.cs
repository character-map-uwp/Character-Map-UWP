using CharacterMap.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls
{
    public sealed class ModalPagePresenter : ContentControl
    {
        public event RoutedEventHandler CloseClicked;

        public object TitleBarContent
        {
            get { return (object)GetValue(TitleBarContentProperty); }
            set { SetValue(TitleBarContentProperty, value); }
        }

        public static readonly DependencyProperty TitleBarContentProperty =
            DependencyProperty.Register(nameof(TitleBarContent), typeof(object), typeof(ModalPagePresenter), new PropertyMetadata(null, (d, e) =>
            {
                ((ModalPagePresenter)d).OnTitleContentChanged();
            }));

        public Brush TitleBackgroundBrush
        {
            get { return (Brush)GetValue(TitleBackgroundBrushProperty); }
            set { SetValue(TitleBackgroundBrushProperty, value); }
        }

        public static readonly DependencyProperty TitleBackgroundBrushProperty =
            DependencyProperty.Register(nameof(TitleBackgroundBrush), typeof(Brush), typeof(ModalPagePresenter), new PropertyMetadata(null));


        public Brush ContentBackground
        {
            get { return (Brush)GetValue(ContentBackgroundProperty); }
            set { SetValue(ContentBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ContentBackgroundProperty =
            DependencyProperty.Register(nameof(ContentBackground), typeof(Brush), typeof(ModalPagePresenter), new PropertyMetadata(null));


        public Visibility CloseButtonVisibility
        {
            get { return (Visibility)GetValue(CloseButtonVisibilityProperty); }
            set { SetValue(CloseButtonVisibilityProperty, value); }
        }

        public static readonly DependencyProperty CloseButtonVisibilityProperty =
            DependencyProperty.Register(nameof(CloseButtonVisibility), typeof(Visibility), typeof(ModalPagePresenter), new PropertyMetadata(Visibility.Visible));


        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ModalPagePresenter), new PropertyMetadata(null));


        public GridLength TitleBarHeight
        {
            get { return (GridLength)GetValue(TitleBarHeightProperty); }
            set { SetValue(TitleBarHeightProperty, value); }
        }

        public static readonly DependencyProperty TitleBarHeightProperty =
            DependencyProperty.Register(nameof(TitleBarHeight), typeof(GridLength), typeof(ModalPagePresenter), new PropertyMetadata(new GridLength(32)));


        public bool AllowShadows
        {
            get { return (bool)GetValue(AllowShadowsProperty); }
            set { SetValue(AllowShadowsProperty, value); }
        }

        public static readonly DependencyProperty AllowShadowsProperty =
            DependencyProperty.Register(nameof(AllowShadows), typeof(bool), typeof(ModalPagePresenter), new PropertyMetadata(true));


        public bool IsWindowRoot
        {
            get { return (bool)GetValue(IsWindowRootProperty); }
            set { SetValue(IsWindowRootProperty, value); }
        }

        public static readonly DependencyProperty IsWindowRootProperty =
            DependencyProperty.Register(nameof(IsWindowRoot), typeof(bool), typeof(ModalPagePresenter), new PropertyMetadata(false));


        public Visibility HeaderVisibility
        {
            get { return (Visibility)GetValue(HeaderVisibilityProperty); }
            set { SetValue(HeaderVisibilityProperty, value); }
        }

        public static readonly DependencyProperty HeaderVisibilityProperty =
            DependencyProperty.Register(nameof(HeaderVisibility), typeof(Visibility), typeof(ModalPagePresenter), new PropertyMetadata(Visibility.Visible));




        public ModalPagePresenter()
        {
            this.DefaultStyleKey = typeof(ModalPagePresenter);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (AllowShadows)
            {
                FrameworkElement tb = (FrameworkElement)this.GetTemplateChild("TitleBackground");
                FrameworkElement cr = (FrameworkElement)this.GetTemplateChild("ContentRoot");
                CompositionFactory.SetThemeShadow(cr, 40, tb);
            }

            if (this.GetTemplateChild("BtnClose") is Button close)
            {
                close.Click -= Close_Click;
                close.Click += Close_Click;
            }

            if (this.GetTemplateChild("TitleBackground") is FrameworkElement f && IsWindowRoot)
            {
                TitleBarHelper.SetTitleBar(f);
                CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
            }

            OnTitleContentChanged();
        }

        public UIElement GetTitleElement()
        {
            return this.GetTemplateChild("TitleHeader") as UIElement;
        }

        private void OnTitleContentChanged()
        {
            if (this.GetTemplateChild("TitleBarPresenter") is ContentPresenter c)
            {
                c.Content = this.TitleBarContent ?? new Border();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            CloseClicked?.Invoke(this, e);
        }

        public void SetTitleBar()
        {
            this.ApplyTemplate();
            if (this.GetTemplateChild("TitleBackground") is FrameworkElement e)
            {
                TitleBarHelper.SetTranisentTitleBar(e);
            }
        }

        public void SetWindowTitleBar()
        {
            this.ApplyTemplate();
            if (this.GetTemplateChild("TitleBackground") is FrameworkElement e)
            {
                e.Measure(new Windows.Foundation.Size(32, 32));
                TitleBarHelper.SetTitleBar(e);
            }
        }

        public void SetDefaultFocus()
        {
            this.ApplyTemplate();
            if (this.GetTemplateChild("BtnClose") is Button close)
            {
                close.Focus(FocusState.Programmatic);
            }
        }

        public void GetAnimationTargets()
        {
            this.ApplyTemplate();
        }
    }
}
