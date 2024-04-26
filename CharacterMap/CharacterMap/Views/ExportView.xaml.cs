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
        ViewModel = new (_fontMap.ViewModel);

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

        List<UIElement> elements = [this, ..OptionsPanel.Children];
        CompositionFactory.PlayEntrance(elements, 0, 200);

        elements = [..PreviewOptions.Children, PreviewContainer];
        CompositionFactory.PlayEntrance(elements, 0, 200);

        elements = [BottomLabel, ..BottomButtonOptions.Children];
        CompositionFactory.PlayEntrance(elements, 0, 200);
    }

    private void ItemsPanel_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
#if DEBUG
        if (sender is CharacterMapCX.Controls.CharacterGridView cgv && cgv.DisableItemClicks is false)
            throw new InvalidOperationException("DisableItemClicks must be true in order to change container tags (these are let to event tokens in C++ when clicks are enabled");
#endif

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
            item.BorderThickness = new (1);
        }
    }

    private void CategoryFlyout_AcceptClicked(object sender, IList<UnicodeRangeModel> e)
    {
        ViewModel.UpdateCategories(e);
    }
}
