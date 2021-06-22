using CharacterMap.Helpers;
using CharacterMap.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace CharacterMap.Views
{
    public sealed partial class ExportView : PopoverViewBase
    {
        public ExportViewModel ViewModel { get; }

        public static void Show(IPopoverPresenter presenter)
        {
            ExportView view = new(presenter);
            presenter.GetPresenter().Child = view;
            view.Show();
        }

        public ExportView(IPopoverPresenter presenter)
        {
            _presenter = presenter;
            _fontMap = presenter.GetFontMap();
            TitleBarHeight = presenter.GetTitleBarHeight();

            ViewModel = new ExportViewModel(_fontMap.ViewModel);

            this.InitializeComponent();
        }

        public void Show()
        {
            this.Visibility = Visibility.Visible;

            // Focus the close button to ensure keyboard focus is retained inside the panel
            BtnClose.Focus(FocusState.Programmatic);

            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            TitleBarHelper.SetTranisentTitleBar(TitleBackground);
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
        }

        private void ContentPanel_Loading(FrameworkElement sender, object args)
        {

        }

        private void ItemsPanel_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.ItemContainer is SelectorItem item && item.Tag == null)
            {
                item.Tag = sender;

                // Set up manual binding to users selected font colour
                item.SetBinding(SelectorItem.ForegroundProperty, new Binding
                {
                    Source = sender,
                    Path = new PropertyPath(nameof(sender.Foreground))
                });

                item.BorderBrush = ResourceHelper.Get<Brush>("ExportBorderBrush");
                item.BorderThickness = new Thickness(1);
            }
        }

        private void CategoryFlyout_AcceptClicked(object sender, IList<UnicodeCategoryModel> e)
        {
            ViewModel.UpdateCategories(e);
        }
    }
}
