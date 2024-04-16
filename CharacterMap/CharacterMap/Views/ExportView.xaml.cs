using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Views;

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

        if (ResourceHelper.AllowAnimation)
            CompositionFactory.SetupOverlayPanelAnimation(this);
    }

    public override void Show()
    {
        ViewModel.GlyphNameModel.SetOptions(ViewModel.Font, ViewModel.Options);
        StartShowAnimation();

        this.Visibility = Visibility.Visible;
        Presenter.SetTitleBar();
        base.Show();
    }

    private void StartShowAnimation()
    {
        if (!ResourceHelper.AllowAnimation)
        {
            this.GetElementVisual().Opacity = 1;
            this.GetElementVisual().Properties.InsertVector3(CompositionFactory.TRANSLATION, Vector3.Zero);
            return;
        }

        List<UIElement> elements = new() { this };
        elements.AddRange(OptionsPanel.Children);
        CompositionFactory.PlayEntrance(elements, 0, 200);

        elements.Clear();
        elements.AddRange(PreviewOptions.Children);
        elements.Add(PreviewContainer);
        CompositionFactory.PlayEntrance(elements, 0, 200);

        elements.Clear();
        elements.Add(BottomLabel);
        elements.AddRange(BottomButtonOptions.Children);
        CompositionFactory.PlayEntrance(elements, 0, 200);
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

    private void CategoryFlyout_AcceptClicked(object sender, IList<UnicodeRangeModel> e)
    {
        ViewModel.UpdateCategories(e);
    }
}
