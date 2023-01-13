using CharacterMap.Controls;
using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using CharacterMap.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Views
{
    public partial class PrintView : PopoverViewBase
    {
        /* 
         * UWP printing requires us to create ALL pages ahead of time
         * as FrameworkElements, before sending them off to the print
         * job all at once. Having too many pages can cause memory 
         * and / or heap crashes, so we limit.
         */
        public static int MAX_PAGE_COUNT = 50;

        public PrintViewModel ViewModel { get; }

        [ObservableProperty] private int _currentPage = 1;

        [ObservableProperty] private int _pageCount = 1;

        [ObservableProperty] private bool _canContinue = true;

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

        private PrintHelper _printHelper = null;


        public static void Show(IPopoverPresenter presenter)
        {
            var view = new PrintView(presenter);
            view.TitleBarHeight = presenter.GetTitleBarHeight();
            presenter.GetPresenter().Child = view;
            view.Show();
        }

        public PrintView(IPopoverPresenter presenter)
        {
            _fontMap = presenter.GetFontMap();
            _presenter = presenter;
            Settings = _fontMap.ViewModel.Settings;
            ViewModel = PrintViewModel.Create(_fontMap.ViewModel);

            if (!DesignMode)
                this.Visibility = Visibility.Collapsed;

            this.InitializeComponent();

            if (ResourceHelper.AllowAnimation)
                CompositionFactory.SetupOverlayPanelAnimation(this);
        }


        public override void Show()
        {

            // Initialize common helper class and register for printing
            _printHelper = new PrintHelper(_fontMap, ViewModel);
            if (_printHelper.RegisterForPrinting() is false)
            {
                Hide();
                return;
            }

            StartShowAnimation();

            this.Visibility = Visibility.Visible;
            UpdatePreview();

            // Focus the close button to ensure keyboard focus is retained inside the panel
            Presenter.SetDefaultFocus();

            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            Presenter.SetTitleBar();

            base.Show();
        }

        public override void Hide()
        {
            base.Hide();

            this.Bindings.StopTracking();

            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;

            _printHelper.UnregisterForPrinting();
            _printHelper.Clear();
        }

        private void StartShowAnimation()
        {
            if (!ResourceHelper.AllowAnimation)
            {
                this.GetElementVisual().Opacity = 1;
                this.GetElementVisual().Properties.InsertVector3(CompositionFactory.TRANSLATION, Vector3.Zero);
                return;
            }

            List<UIElement> elements = new () { this };
            elements.AddRange(OptionsPanel.Children);
            CompositionFactory.PlayEntrance(elements, 0, 200);

            elements.Clear();
            elements.AddRange(PreviewOptions.Children);
            elements.Add(PreviewViewBox);
            CompositionFactory.PlayEntrance(elements, 0, 200);

            elements.Clear();
            elements.Add(BottomLabel);
            elements.AddRange(BottomButtonOptions.Children);
            CompositionFactory.PlayEntrance(elements, 0, 200);
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

            CompositionFactory.SetThemeShadow(view, 30, ContentBackground);
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
