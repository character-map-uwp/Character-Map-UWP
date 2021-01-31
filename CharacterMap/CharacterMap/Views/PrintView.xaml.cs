using CharacterMap.Controls;
using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.ViewModels;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Views
{
    public interface IPrintPresenter
    {
        Border GetPresenter();
        FontMapView GetFontMap();
        GridLength GetTitleBarHeight();
    }

    public sealed partial class PrintView : ViewBase
    {
        /* 
         * UWP printing requires us to create ALL pages ahead of time
         * as FrameworkElements, before sending them off to the print
         * job all at once. Having too many pages can cause memory 
         * and / or heap crashes, so we limit.
         */
        public static int MAX_PAGE_COUNT = 50;

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

        private bool _canContinue = true;
        public bool CanContinue
        {
            get => _canContinue;
            private set => Set(ref _canContinue, value);
        }

        private GridLength _titleBarHeight = new GridLength(32);
        public GridLength TitleBarHeight
        {
            get => _titleBarHeight;
            set => Set(ref _titleBarHeight, value);
        }

        public List<PrintLayout> Layouts { get; } = new List<PrintLayout>
        {
            PrintLayout.Grid,
            PrintLayout.List,
            //PrintLayout.TwoColumn
        };

        public List<GlyphAnnotation> Annotations { get; } = new List<GlyphAnnotation>
        {
            GlyphAnnotation.None,
            GlyphAnnotation.UnicodeHex,
            GlyphAnnotation.UnicodeIndex
        };

        private Debouncer _sizeDebouncer { get; } = new Debouncer();

        public AppSettings Settings { get; }

        private IPrintPresenter _presenter = null;

        private PrintHelper _printHelper = null;

        private FontMapView _fontMap = null;

        public static void Show(IPrintPresenter presenter)
        {
            var view = new PrintView(presenter);
            view.TitleBarHeight = presenter.GetTitleBarHeight();
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
            Composition.SetupOverlayPanelAnimation(this);
        }

        public void Show()
        {
            // Initialize common helper class and register for printing
            _printHelper = new PrintHelper(_fontMap, ViewModel);
            _printHelper.RegisterForPrinting();

            StartShowAnimation();

            this.Visibility = Visibility.Visible;
            UpdatePreview();

            // Focus the close button to ensure keyboard focus is retained inside the panel
            BtnClose.Focus(FocusState.Programmatic);

            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        public void Hide()
        {
            if (_presenter == null)
                return;

            this.Bindings.StopTracking();

            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;

            _presenter.GetPresenter().Child = null;
            _presenter = null;
            _fontMap = null;

            _printHelper.UnregisterForPrinting();
            _printHelper.Clear();

        }

        private void StartShowAnimation()
        {
            if (!Composition.UISettings.AnimationsEnabled)
            {
                this.GetElementVisual().Opacity = 1;
                this.GetElementVisual().Properties.InsertVector3(Composition.TRANSLATION, Vector3.Zero);
                return;
            }

            List<UIElement> elements = new List<UIElement> { this };
            elements.AddRange(OptionsPanel.Children);
            Composition.PlayEntrance(elements, 0, 200);

            elements.Clear();
            elements.AddRange(PreviewOptions.Children);
            elements.Add(PreviewViewBox);
            Composition.PlayEntrance(elements, 0, 200);

            elements.Clear();
            elements.Add(BottomLabel);
            elements.AddRange(BottomButtonOptions.Children);
            Composition.PlayEntrance(elements, 0, 200);
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                // For slider based values, we use a grace period before updating the preview
                case nameof(ViewModel.GlyphSize):
                case nameof(ViewModel.VerticalMargin):
                case nameof(ViewModel.HorizontalMargin):
                    _sizeDebouncer.Debounce(350, UpdateDisplay);
                    break;

                case nameof(ViewModel.FirstPage):
                case nameof(ViewModel.PagesToPrint):
                    // Do not update preview.
                    // Page being previewed might be outside the range, don't care.
                    break;

                // For other values we update straight away
                default:
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

            bool isMaxRange = ViewModel.PagesToPrint == GetMaxPageRange(0, 0);

            // Calculate how many pages would be taken up by the current selection
            Size safeSize = size.GetSafeAreaSize(ViewModel.Orientation);
            int perPage = FontMapPrintPage.CalculateGlyphsPerPage(safeSize, ViewModel);
            PageCount = Math.Max((int)Math.Ceiling((double)ViewModel.Characters.Count / (double)perPage), 1);
            CurrentPage = Math.Max(Math.Min(CurrentPage, PageCount), 1);

            // Ensure print range is clamped
            if (ViewModel.FirstPage > PageCount)
            {
                ViewModel.FirstPage = Math.Max(1, PageCount - ViewModel.PagesToPrint);
                ViewModel.PagesToPrint = PageCount;
            }
            if (ViewModel.FirstPage + ViewModel.PagesToPrint - 1 > PageCount)
                ViewModel.PagesToPrint = PageCount;

            if (isMaxRange)
                ViewModel.PagesToPrint = GetMaxPageRange(0, 0);

            if (view == null)
                view = new FontMapPrintPage(ViewModel, _fontMap.CharGrid.ItemTemplate, true)
                {
                    Background = ResourceHelper.Get<Brush>("WhiteBrush")
                };
            
            view.ClearCharacters();

            Size pageSize = size.GetPageSize(ViewModel.Orientation);
            view.Width = pageSize.Width;
            view.Height = pageSize.Height;

            view.PrintableArea.Width = safeSize.Width;
            view.PrintableArea.Height = safeSize.Height;

            view.AddCharacters(CurrentPage - 1, perPage, ViewModel.Characters);
            view.Update();

            CanContinue = ViewModel.Characters.Count > 0;

            Composition.SetThemeShadow(view, 30, ContentBackground);
            PreviewViewBox.Child = view;
        }




        /* UI EVENT HANDLERS */

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

        private void ContentPanel_Loading(FrameworkElement sender, object args)
        {
            Composition.SetThemeShadow(ContentPanel, 40, TitleBackground);
        }

        private void CategoryFlyout_AcceptClicked(object sender, IList<UnicodeCategoryModel> e)
        {
            ViewModel.UpdateCategories(e);
        }




        /* CONVERTERS */

        public string GetPageRangeLabel(int start, int count)
        {
            return Localization.Get("PrintViewPrintingLabel", start, start + count - 1);
        }

        public string GetLastPageLabel(int start, int count)
        {
            return $"{start + count - 1}";
        }

        public Visibility IsOutOfRange(int start, int count, int current)
        {
            int last = start + count - 1;
            return current >= start && current <= last ? Visibility.Collapsed : Visibility.Visible;
        }

        public int GetLastPage(int start, int count)
        {
            return start + count - 1;
        }

        public int GetMaxPageRange(int r, int s)
        {
            int range = Math.Min(PageCount + 1 - ViewModel.FirstPage, MAX_PAGE_COUNT);
            return range;
        }
    }
}
