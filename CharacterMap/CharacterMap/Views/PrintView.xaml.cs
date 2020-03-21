using CharacterMap.Controls;
using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.ViewModels;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace CharacterMap.Views
{
    public interface IPrintPresenter
    {
        Border GetPresenter();
        FontMapView GetFontMap();
    }

    public sealed partial class PrintView : ViewBase
    {
        public PrintViewModel ViewModel { get; }

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            private set => Set(ref _currentPage, value);
        }

        private int _pageCount = 1;
        public int PageCount
        {
            get => _pageCount;
            private set => Set(ref _pageCount, value);
        }

        public List<PrintLayout> Layouts { get; } = new List<PrintLayout>
        {
            PrintLayout.Grid,
            PrintLayout.List,
            PrintLayout.TwoColumn
        };

        private Debouncer _sizeDebouncer { get; } = new Debouncer();

        public AppSettings Settings { get; }

        private IPrintPresenter _presenter = null;

        private PrintHelper _printHelper = null;

        private FontMapView _fontMap = null;

        public static void Show(IPrintPresenter presenter)
        {
            var view = new PrintView(presenter);
            presenter.GetPresenter().Child = view;
            view.Show();
        }

        public PrintView(IPrintPresenter presenter)
        {
            _fontMap = presenter.GetFontMap();
            _presenter = presenter;
            Settings = _fontMap.ViewModel.Settings;
            ViewModel = PrintViewModel.Create(_fontMap.ViewModel);

            if (!DesignMode.DesignMode2Enabled)
                this.Visibility = Visibility.Collapsed;

            this.InitializeComponent();
            SetupAnimation();
        }

        private void SetupAnimation()
        {
            Visual v = this.EnableTranslation(true).GetElementVisual();

            var t = v.Compositor.CreateVector3KeyFrameAnimation();
            t.Target = Composition.TRANSLATION;
            t.InsertKeyFrame(1, new Vector3(0, 200, 0));
            t.Duration = TimeSpan.FromSeconds(0.375);

            var o = Composition.CreateFade(v.Compositor, 0, null, 200);
            this.SetHideAnimation(v.Compositor.CreateAnimationGroup(t, o));
            this.SetShowAnimation(Composition.CreateEntranceAnimation(this, new Vector3(0, 200, 0), 0, 550));
        }

        public void Show()
        {
            // Initialize common helper class and register for printing
            _printHelper = new PrintHelper(_fontMap, ViewModel);
            _printHelper.RegisterForPrinting();

            this.Visibility = Visibility.Visible;
            UpdatePreview();

            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        public void Hide()
        {
            if (_presenter == null)
                return;

            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;

            _presenter.GetPresenter().Child = null;
            _presenter = null;
            _fontMap = null;

            _printHelper.UnregisterForPrinting();
            _printHelper.Clear();
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.GlyphSize):
                case nameof(ViewModel.VerticalMargin):
                case nameof(ViewModel.HorizontalMargin):
                case nameof(ViewModel.Layout):
                    _sizeDebouncer.Debounce(350, UpdateDisplay);
                    break;
                case nameof(ViewModel.Orientation):
                    UpdateDisplay();
                    break;
            }
        }

        private void UpdateDisplay()
        {
            if (PreviewViewBox.Child is FontMapPrintPage page)
                UpdatePreview(page);
        }

        private void UpdatePreview(FontMapPrintPage view = null)
        {
            PrintSize size = PrintSize.CreateA4();
            size.HorizontalMargin = ViewModel.HorizontalMargin;
            size.VerticalMargin = ViewModel.VerticalMargin;

            Size safeSize = size.GetSafeAreaSize(ViewModel.Orientation);
            int perPage = FontMapPrintPage.CalculateGlyphsPerPage(safeSize, ViewModel);
            PageCount = (int)Math.Ceiling((double)ViewModel.Font.Characters.Count / (double)perPage);
            CurrentPage = Math.Min(CurrentPage, PageCount);

            if (view == null)
            {
                view = new FontMapPrintPage(ViewModel, _fontMap.CharGrid.ItemTemplate, true)
                {
                    Background = ResourceHelper.Get<Brush>("WhiteBrush")
                };
            }
            else
            {
                view.ClearCharacters();
            }

            Size pageSize = size.GetPageSize(ViewModel.Orientation);
            view.Width = pageSize.Width;
            view.Height = pageSize.Height;

            view.PrintableArea.Width = safeSize.Width;
            view.PrintableArea.Height = safeSize.Height;

            view.AddCharacters(CurrentPage - 1, perPage, _fontMap.ViewModel.Chars);
            view.Update();

            Composition.SetThemeShadow(view, 30, ContentBackground);
            PreviewViewBox.Child = view;
        }


        private void BtnContinue_Click(object sender, RoutedEventArgs e)
        {
            _ = _printHelper.ShowPrintUIAsync();
        }

        private void RadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox bs)
            {
                ViewModel.Orientation = bs.SelectedIndex == 0 ? Orientation.Vertical : Orientation.Horizontal;
            }
        }

        private void NumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            _sizeDebouncer.Debounce(350, UpdateDisplay);
        }
    }
}
