using CharacterMap.Controls;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Views;

[DependencyProperty<int>("GridSize")]
public sealed partial class SettingsView : ViewBase
{
    public AppSettings Settings { get; }

    public SettingsViewModel ViewModel { get; }

    public bool IsOpen { get; private set; }

    [ObservableProperty]
    private GridLength _titleBarHeight = new(32);

    private bool _themeSupportsShadows = false;

    private bool _themeSupportsDark = false;

    private NavigationHelper _navHelper { get; } = new();

    private int _requested = 0;

    public SettingsView()
    {
        this.InitializeComponent();

        if (DesignMode)
            return;

        ViewModel = new();
        Settings = ResourceHelper.AppSettings;
        Register<AppSettingsChangedMessage>(OnAppSettingsUpdated);
        Register<FontListCreatedMessage>(m => UpdateExport());

        GridSize = Settings.GridSize;

        _themeSupportsShadows = ResourceHelper.SupportsShadows();
        _themeSupportsDark = ResourceHelper.Get<Boolean>("SupportsDarkTheme");

        _navHelper.BackRequested += (s, e) => Hide();
    }

    public void Show(CharacterRenderingOptions options, InstalledFont font, int idx = 0)
    {
        if (IsOpen)
        {
            SetIndex(idx);
            return;
        }

        UpdateAnimation();
        StartShowAnimation();
        this.Visibility = Visibility.Visible;

        if (!ResourceHelper.AllowAnimation)
        {
            this.GetElementVisual().Opacity = 1;
            this.GetElementVisual().SetTranslation(0, 0, 0);
        }

        // 1. Focus the close button to ensure keyboard focus is retained inside the settings panel
        Presenter.SetDefaultFocus();

        // 2. Reset scroll position
#pragma warning disable CS0618 // ChangeView doesn't work well when not properly visible
        ContentScroller.ScrollToVerticalOffset(0);
#pragma warning restore CS0618

        // 3. Get the fonts used for Font List & Character Grid previews
        ViewModel.UpdatePreviews(font, options);


        Presenter.SetTitleBar();
        IsOpen = true;
        _navHelper.Activate();

        _requested = idx;
        SetIndex(idx);

        void SetIndex(int i)
        {
            if (IsLoaded)
                MenuColumn.Children.OfType<MenuButton>().ElementAt(i).IsChecked = true;
        }

    }

    public void Hide()
    {
        UpdateAnimation();
        _navHelper.Deactivate();

        TitleBarHelper.RestoreDefaultTitleBar();
        IsOpen = false;
        this.Visibility = Visibility.Collapsed;
        Messenger.Send(new ModalClosedMessage());
    }

    void OnAppSettingsUpdated(AppSettingsChangedMessage msg)
    {
        RunOnUI(() =>
        {
            switch (msg.PropertyName)
            {
                case nameof(Settings.UserRequestedTheme):
                    OnPropertyChanged(nameof(Settings));
                    break;
                case nameof(Settings.GridSize):
                    // We can't direct bind here as it may be updated from a different UI Thread.
                    GridSize = Settings.GridSize;
                    break;
                case nameof(Settings.ApplicationDesignTheme):
                    //UpdateStyle();
                    break;
            }
        });
    }

    protected override void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Override base so we do not unregister messages
    }

    private void View_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateStyle();
        ((RadioButton)MenuColumn.Children.ElementAt(_requested)).IsChecked = true;
    }


    /* Work around to avoid binding threading issues */
    private int GetGridSize(int s) => s;

    private void UpdateGridSize(double d)
    {
        GridSize = Settings.GridSize = (int)d;
    }

    private void UpdateExport()
    {
        this.RunOnUI(() =>
        {
            ExportImportedButton?.SetVisible(FontFinder.ImportedFonts.Count > 0);
        });
    }

    private void FontNamingSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Settings.ExportNamingScheme = (ExportNamingScheme)((RadioButtons)sender).SelectedIndex;
    }

    private void UseSystemFont_Checked(object sender, RoutedEventArgs e)
    {
        Settings.UseFontForPreview = false;
        ViewModel.ResetFontPreview();
    }

    private void UseActualFont_Checked(object sender, RoutedEventArgs e)
    {
        Settings.UseFontForPreview = true;
        ViewModel.ResetFontPreview();
    }

    private void Design_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SetDesign(((ComboBox)sender).SelectedIndex);
    }

    public void SelectedLanguageToString(object selected) =>
        Settings.AppLanguage = selected is SupportedLanguage s ? s.LanguageID : "";

    private void DeleteRampClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement ele
            && ele.DataContext is string s)
        {
            ViewModel.RemoveRamp(s);
        }
    }

    private void SearchSwitchSettingsPresenter_Loaded(object sender, RoutedEventArgs e)
    {
        // Fix bug with x:Load
        if (Settings.UseInstantSearch)
            this.FindName(nameof(SearchDelayItem));
    }

    private void MenuItem_Checked(object sender, RoutedEventArgs e)
    {
        Panel GetPanel(RadioButton button)
        {
            return button.Tag switch
            {
                Panel p => p,
                string s => ContentPanel.FindName(s) as Panel,
                _ => null
            };
        }

        if (sender is RadioButton item
          && GetPanel(item) is Panel panel
          && panel.Visibility == Visibility.Collapsed)
        {
            // 1: Ensure all settings panels are hidden
            foreach (var child in ContentPanel.Children.OfType<FrameworkElement>())
            {
                // 2: Deactivate old content if supported
                if (child.Visibility == Visibility.Visible
                    && child is Panel p
                    && p.Children.Count == 1
                    && p.Children[0] is IActivateableControl d)
                    d.Deactivate();

                child.Visibility = Visibility.Collapsed;
            }

            // 3: Reset scroll position
            ContentScroller.ChangeView(null, 0, null, true);

            // 4: Activate new content if supported
            if (panel.Children.Count == 1 && panel.Children[0] is IActivateableControl a)
            {
                a.Activate();
                VisualStateManager.GoToState(this, ContentScrollDisabledState.Name, false);
            }
            else
                VisualStateManager.GoToState(this, ContentScrollEnabledState.Name, false);

            panel.Opacity = 0;
            panel.Visibility = Visibility.Visible;
            panel.Measure(ContentScroller.DesiredSize);

            // 5. Update properties that can't be set with bindings
            //    and can't be set earlier due to x:Load
            if (panel == UserInterfacePanel)
            {
                if (Settings.UseFontForPreview)
                    UseActualFont.IsChecked = true;
                else
                    UseSystemFont.IsChecked = true;
            }
            else if (panel == UserInterfaceAdvancedPanel)
            {
                if (_themeSupportsDark && ThemeSystem is not null)
                {
                    switch (Settings.UserRequestedTheme)
                    {
                        case ElementTheme.Default:
                            ThemeSystem.IsChecked = true;
                            break;
                        case ElementTheme.Light:
                            ThemeLight.IsChecked = true;
                            break;
                        case ElementTheme.Dark:
                            ThemeDark.IsChecked = true;
                            break;
                    }
                }
            }
            else if (panel == ExportPanel)
            {
                FontNamingSelection.SelectedIndex = (int)Settings.ExportNamingScheme;
            }
            else if (panel == FontPanel)
            {
                UpdateExport();
            }

            // 6. Start child animation
            if (ResourceHelper.AllowAnimation)
            {
                if (ThemeStatesGroup.CurrentState == ZuneThemeState)
                    CompositionFactory.PlayEntrance(GetChildren(panel), 10, 0, 140, staggerMs: 120);
                else
                    CompositionFactory.PlayEntrance(GetChildren(panel), 10, 80);
            }

            // 7. Show selected panel
            panel.Opacity = 1;
        }
    }

    private List<UIElement> GetChildren(Panel p)
    {
        List<UIElement> children = new();

        foreach (var child in p.Children)
        {
            if (child is ItemsControl c
                && c is not SettingsPresenter
                && c.ItemsPanelRoot is not null)
            {
                c.Realize(p.DesiredSize.Width, p.DesiredSize.Height);
                children.AddRange(c.ItemsPanelRoot.Children);
            }
            else
                children.Add(child);
        }

        return children;
    }


    void UpdateStyle()
    {
        if (DesignMode) return;
        //string key = Settings.ApplicationDesignTheme == 0 ? "Default" : "FUI";
        //if (Settings.ApplicationDesignTheme == 2) key = "Zune";
        //var controls = this.GetDescendants().OfType<FrameworkElement>().Where(e => e is not IThemeableControl && Properties.GetStyleKey(e) is not null).ToList();
        //foreach (var p in controls)
        //{
        //    string target = $"{key}{Properties.GetStyleKey(p)}";
        //    Style style = ResourceHelper.Get<Style>(this, target);
        //    p.Style = style;
        //}

        //ResourceHelper.SendThemeChanged();

        this.GoToThemeState();
    }




    /* ANIMATION */

    private void UpdateAnimation()
    {
        if (ResourceHelper.AllowAnimation)
        {
            if (ResourceHelper.IsZuneTheme())
                CompositionFactory.SetupOverlayPanelAnimationX(this);
            else
                CompositionFactory.SetupOverlayPanelAnimation(this);
        }
        else
        {
            this.SetShowAnimation(null);
            this.SetHideAnimation(null);
        }
    }

    private void StartShowAnimation()
    {
        if (!ResourceHelper.AllowAnimation)
            return;

        List<UIElement> elements = new() { this, MenuColumn, ContentBorder };
        if (ResourceHelper.IsZuneTheme())
            CompositionFactory.PlayEntrance(elements, 0, 0, 40);
        else if (ResourceHelper.IsMaterialTheme)
            CompositionFactory.PlayScaleEntrance(this, 0.8f, 1, 0.3f);
        else
            CompositionFactory.PlayEntrance(elements, 0, 200);

        UpdateStyle();
    }




    /* CONVERTERS */

    Visibility ShowUnicode(GlyphAnnotation annotation)
    {
        return annotation != GlyphAnnotation.None ? Visibility.Visible : Visibility.Collapsed;
    }
}
