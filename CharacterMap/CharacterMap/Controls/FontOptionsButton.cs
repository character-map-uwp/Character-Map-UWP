using CharacterMap.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;

internal sealed partial class FontAxisEditorItem : ObservableObject
{
    private readonly Action<FontAxisEditorItem> _onChanged;

    public DWriteFontAxis Axis { get; }
    public string Label => Axis.Label;
    public double Minimum => Axis.Minimum;
    public double Maximum => Axis.Maximum;
    public Visibility HiddenIconVisibility => Axis.IsHidden ? Visibility.Visible : Visibility.Collapsed;
    public ICommand ResetCommand { get; }

    public double Value
    {
        get => Axis.Value;
        set
        {
            float clamped = (float)Math.Clamp(value, Axis.Minimum, Axis.Maximum);
            if (Math.Abs(Axis.Value - clamped) < 0.0001f)
                return;

            Axis.Value = clamped;
            OnPropertyChanged();
            _onChanged?.Invoke(this);
        }
    }

    public FontAxisEditorItem(DWriteFontAxis axis, Action<FontAxisEditorItem> onChanged)
    {
        Axis = axis;
        _onChanged = onChanged;
        ResetCommand = new RelayCommand(() => Value = Axis.DefaultValue);
    }
}

[TemplatePart(Name = PartRootButton, Type = typeof(Button))]
[TemplatePart(Name = PartFlyoutHost, Type = typeof(ContentControl))]
[DependencyProperty<CharacterRenderingOptions>("SelectedItem", null, nameof(OnSelectedItemChanged))]
[DependencyProperty<string>("PlaceholderText", "Select font", nameof(UpdateDisplayText))]
[DependencyProperty<string>("DisplayText", "Select font")]
public sealed partial class FontOptionsButton : Control
{
    private const string PartRootButton = "RootButton";
    private const string PartFlyoutHost = "FlyoutHost";

    public event EventHandler<CharacterRenderingOptions> SelectedItemChanged;

    private Button _rootButton;
    private Flyout _flyout;
    private ContentControl _flyoutHost;
    private ComboBox _familyComboBox;
    private ComboBox _faceComboBox;
    private ItemsControl _axisItemsControl;
    private ToggleSwitch _colorGlyphToggle;
    private ComboBox _typographyComboBox;

    private bool _isSyncing;
    private bool _flyoutPartsWired;
    private CMFontFamily _selectedFamily;
    private CMFontFace _selectedFace;
    private FaceAnalysisModel _faceAnalysis;
    private IReadOnlyList<DWriteFontAxis> _currentAxes = [];
    private IReadOnlyList<TypographyFeatureInfo> _currentTypography = [];

    public FontOptionsButton()
    {
        this.DefaultStyleKey = typeof(FontOptionsButton);
        UpdateDisplayText();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_flyout is not null)
            _flyout.Opening -= OptionsFlyout_Opening;

        _rootButton = GetTemplateChild(PartRootButton) as Button;
        _flyout = _rootButton?.Flyout as Flyout;
        if (_flyout is not null)
        {
            _flyout.Opening += OptionsFlyout_Opening;
            _flyoutHost = _flyout.Content as ContentControl;
        }
        else
            _flyoutHost = GetTemplateChild(PartFlyoutHost) as ContentControl;

        _flyoutPartsWired = false;
        UpdateDisplayText();
        SyncUIFromSelectedItem();
    }

    private bool EnsureFlyoutParts()
    {
        if (_flyoutHost is null)
            return false;

        if (_flyoutPartsWired)
            return true;

        _flyoutHost.ApplyTemplate();
        if (VisualTreeHelper.GetChildrenCount(_flyoutHost) == 0)
            return false;

        if (VisualTreeHelper.GetChild(_flyoutHost, 0) is not FrameworkElement root)
            return false;

        _familyComboBox = root.FindName("FamilyComboBox") as ComboBox;
        _faceComboBox = root.FindName("FaceComboBox") as ComboBox;
        _axisItemsControl = root.FindName("AxisItemsControl") as ItemsControl;
        _colorGlyphToggle = root.FindName("ColorGlyphToggle") as ToggleSwitch;
        _typographyComboBox = root.FindName("TypographyComboBox") as ComboBox;

        if (_familyComboBox is not null)
            _familyComboBox.SelectionChanged += FamilyComboBox_SelectionChanged;

        if (_faceComboBox is not null)
            _faceComboBox.SelectionChanged += FaceComboBox_SelectionChanged;

        if (_colorGlyphToggle is not null)
            _colorGlyphToggle.Toggled += ColorGlyphToggle_Toggled;

        if (_typographyComboBox is not null)
            _typographyComboBox.SelectionChanged += TypographyComboBox_SelectionChanged;

        _flyoutPartsWired = true;
        return true;
    }

    private void OnSelectedItemChanged()
    {
        UpdateDisplayText();

        if (_isSyncing)
            return;

        SyncUIFromSelectedItem();
        SelectedItemChanged?.Invoke(this, SelectedItem);
    }

    private void UpdateDisplayText()
    {
        if (SelectedItem?.Family is { } fam)
            DisplayText = fam.Name;
        else if (SelectedItem?.Face is { } face)
            DisplayText = face.FamilyName;
        else
            DisplayText = PlaceholderText ?? "Select font";
    }

    private void OptionsFlyout_Opening(object sender, object e)
    {
        EnsureFlyoutParts();
        SyncUIFromSelectedItem();
    }

    private void SyncUIFromSelectedItem()
    {
        if (!EnsureFlyoutParts())
            return;

        _isSyncing = true;
        try
        {
            if (_familyComboBox is not null && _familyComboBox.ItemsSource is null)
                _familyComboBox.ItemsSource = FontFinder.Fonts;

            CharacterRenderingOptions item = SelectedItem;
            if (item is null)
            {
                _selectedFamily = null;
                _selectedFace = null;
                _faceAnalysis = null;
                _currentAxes = [];
                _currentTypography = [];

                if (_familyComboBox is not null)
                    _familyComboBox.SelectedItem = null;
                if (_faceComboBox is not null)
                    _faceComboBox.ItemsSource = null;
                if (_axisItemsControl is not null)
                    _axisItemsControl.ItemsSource = null;
                if (_typographyComboBox is not null)
                    _typographyComboBox.ItemsSource = null;

                UpdateFlyoutVisualStates(false, false, false);
                return;
            }

            CMFontFamily targetFamily = item.Family;
            if (targetFamily is null && item.Face is { } f)
                targetFamily = FontFinder.Fonts?.FirstOrDefault(fam => fam.Name == f.FamilyName || fam.RealFaces.Contains(f));

            _selectedFamily = targetFamily;
            if (_familyComboBox is not null)
                _familyComboBox.SelectedItem = targetFamily;

            IList<CMFontFace> faces = GetFacesForFamily(targetFamily, item.Face);
            if (_faceComboBox is not null)
                _faceComboBox.ItemsSource = faces;

            CMFontFace targetFace = item.Face ?? targetFamily?.DefaultFace ?? faces.FirstOrDefault();
            _selectedFace = targetFace;
            if (_faceComboBox is not null)
                _faceComboBox.SelectedItem = targetFace;

            LoadFaceCapabilities(targetFace, item.Axis, item.ColorRenderOption, item.DXTypography);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private static IList<CMFontFace> GetFacesForFamily(CMFontFamily family, CMFontFace fallbackFace)
    {
        if (family is not null)
        {
            if (family.RealFaces.Count > 0)
                return family.RealFaces;

            if (family.DefaultFace is { } def)
                return [def];
        }

        return fallbackFace is not null ? [fallbackFace] : [];
    }

    private void LoadFaceCapabilities(
        CMFontFace face,
        IReadOnlyList<DWriteFontAxis> axisOverrides,
        DWriteColorRenderOption colorOption,
        TypographyFeatureInfo selectedTypography)
    {
        if (face is null)
        {
            _faceAnalysis = null;
            _currentAxes = [];
            _currentTypography = [];
            if (_axisItemsControl is not null)
                _axisItemsControl.ItemsSource = null;
            if (_typographyComboBox is not null)
                _typographyComboBox.ItemsSource = null;
            UpdateFlyoutVisualStates(false, false, false);
            return;
        }

        _faceAnalysis = new FaceAnalysisModel(face, true);

        IReadOnlyList<DWriteFontAxis> axes = _faceAnalysis.VariationAxis?.Copy() ?? [];
        if (axisOverrides is { Count: > 0 })
        {
            foreach (DWriteFontAxis axis in axes)
            {
                DWriteFontAxis match = axisOverrides.FirstOrDefault(a => a.Tag == axis.Tag);
                if (match is not null)
                    axis.Value = Math.Clamp(match.Value, axis.Minimum, axis.Maximum);
            }
        }

        _currentAxes = axes;
        List<FontAxisEditorItem> axisEditors = axes.Select(a => new FontAxisEditorItem(a, _ => CommitSelectedItemFromUI())).ToList();
        if (_axisItemsControl is not null)
            _axisItemsControl.ItemsSource = axisEditors;

        bool hasAxes = _currentAxes.Count > 0;
        bool hasColor = _faceAnalysis.Analysis?.ContainsVectorColorGlyphs == true;
        if (_colorGlyphToggle is not null)
            _colorGlyphToggle.IsOn = colorOption != DWriteColorRenderOption.Monochrome;

        IReadOnlyList<TypographyFeatureInfo> typography = face.TypographyFeatures ?? [];
        _currentTypography = typography;
        bool hasTypography = _currentTypography.Count > 1;
        if (_typographyComboBox is not null)
        {
            _typographyComboBox.ItemsSource = _currentTypography;
            TypographyFeatureInfo matchedTypo = selectedTypography is not null
                ? _currentTypography.FirstOrDefault(t => t.Feature == selectedTypography.Feature)
                : null;
            _typographyComboBox.SelectedItem = matchedTypo ?? _currentTypography.FirstOrDefault();
        }

        UpdateFlyoutVisualStates(hasAxes, hasColor, hasTypography);
    }

    private void UpdateFlyoutVisualStates(bool hasAxes, bool hasColor, bool hasTypography)
    {
        if (_flyoutHost is null)
            return;

        VisualStateManager.GoToState(_flyoutHost, hasAxes ? "HasAxesState" : "NoAxesState", false);
        VisualStateManager.GoToState(_flyoutHost, hasColor ? "HasColorState" : "NoColorState", false);
        VisualStateManager.GoToState(_flyoutHost, hasTypography ? "HasTypographyState" : "NoTypographyState", false);
    }

    private void CommitSelectedItemFromUI()
    {
        if (_isSyncing || _selectedFace is null)
            return;

        _isSyncing = true;
        try
        {
            List<TypographyFeatureInfo> typography = _typographyComboBox?.SelectedItem is TypographyFeatureInfo selectedTypo
                ? [selectedTypo]
                : [TypographyFeatureInfo.None];

            bool hasColor = _faceAnalysis?.Analysis?.ContainsVectorColorGlyphs == true;
            DWriteColorRenderOption colorOption = (hasColor && _colorGlyphToggle is { IsOn: false })
                ? DWriteColorRenderOption.Monochrome
                : DWriteColorRenderOption.Default;

            float fontSize = SelectedItem?.FontSize > 0 ? SelectedItem.FontSize : 64.0f;

            DWriteFontFace activeFace = (_currentAxes.Count > 0 || _selectedFace.DirectWriteProperties.Simulations != Windows.UI.Xaml.Media.StyleSimulations.None)
                ? _selectedFace.Face.CreateVariant(_selectedFace.DirectWriteProperties.Simulations, _currentAxes)
                : _selectedFace.Face;

            CharacterRenderingOptions updated = new(_selectedFace, typography, fontSize, null, _currentAxes)
            {
                Family = _selectedFamily,
                ActiveFontFace = activeFace,
                ColorRenderOption = colorOption
            };

            SelectedItem = updated;
            UpdateDisplayText();
            SelectedItemChanged?.Invoke(this, updated);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void FamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncing)
            return;

        if (_familyComboBox?.SelectedItem is not CMFontFamily family)
            return;

        _isSyncing = true;
        try
        {
            _selectedFamily = family;
            IList<CMFontFace> faces = GetFacesForFamily(family, null);
            if (_faceComboBox is not null)
                _faceComboBox.ItemsSource = faces;

            _selectedFace = family.DefaultFace ?? faces.FirstOrDefault();
            if (_faceComboBox is not null)
                _faceComboBox.SelectedItem = _selectedFace;

            LoadFaceCapabilities(_selectedFace, null, DWriteColorRenderOption.Default, TypographyFeatureInfo.None);
        }
        finally
        {
            _isSyncing = false;
        }

        CommitSelectedItemFromUI();
    }

    private void FaceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncing)
            return;

        if (_faceComboBox?.SelectedItem is not CMFontFace face)
            return;

        _isSyncing = true;
        try
        {
            _selectedFace = face;
            DWriteColorRenderOption currentColor = (_colorGlyphToggle?.IsOn ?? true)
                ? DWriteColorRenderOption.Default
                : DWriteColorRenderOption.Monochrome;
            TypographyFeatureInfo currentTypo = _typographyComboBox?.SelectedItem as TypographyFeatureInfo;
            LoadFaceCapabilities(face, null, currentColor, currentTypo);
        }
        finally
        {
            _isSyncing = false;
        }

        CommitSelectedItemFromUI();
    }

    private void ColorGlyphToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isSyncing)
            return;

        CommitSelectedItemFromUI();
    }

    private void TypographyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncing)
            return;

        CommitSelectedItemFromUI();
    }
}

