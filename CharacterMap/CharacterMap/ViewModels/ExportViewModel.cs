﻿using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;

namespace CharacterMap.ViewModels;

public class ExportViewModel : ViewModelBase
{
    #region Properties

    public List<String> ExportFormats { get; } = ["PNG", "SVG"];

    protected override bool TrackAnimation => true;

    public GlyphFileNameViewModel GlyphNameModel { get; } = new();

    public CMFontFamily Font { get; }
    public CMFontFace Variant { get; set; }

    public CharacterRenderingOptions Options { get; set; }

    public IReadOnlyList<Character> Characters { get => GetV<IReadOnlyList<Character>>(); private set => Set(value); }
    public IList<UnicodeRangeModel> Categories { get => GetV<IList<UnicodeRangeModel>>(); private set => Set(value); }

    public bool HideWhitespace { get => GetV(true); set => Set(value); }
    public bool SkipBlankGlyphs { get => GetV(true); set => Set(value); }
    public double GlyphSize { get => GetV(0d); set => Set(value); }
    public Color GlyphColor { get => GetV(Colors.White); set => Set(value); }
    public bool ExportColor { get => GetV(true); set => Set(value); }
    public bool IsWhiteChecked { get => GetV(false); set => Set(value); }
    public bool IsBlackChecked { get => GetV(false); set => Set(value); }
    public bool IsExporting { get => GetV(false); set => Set(value); }
    public bool ShowCancelExport { get => GetV(false); set => Set(value); }
    public int SelectedFormat { get => GetV((int)ExportFormat.Png); set => Set(value); }
    public string ExportMessage { get => Get<string>(); set => Set(value); }
    public string Summary { get => Get<string>(); private set => Set(value); }
    public string SelectedText { get => Get<string>(); private set => Set(value); }
    public ElementTheme PreviewTheme { get => GetV(ResourceHelper.GetEffectiveTheme()); set => Set(value); }

    public bool CanContinue => Characters.Count > 0;
    public bool IsPngFormat => SelectedFormat == (int)ExportFormat.Png;

    #endregion

    private CancellationTokenSource _currentToken = null;

    DispatcherQueue _dispatcherQueue { get; }

    public ExportViewModel(FontMapViewModel viewModel)
    {
        Font = viewModel.SelectedFont.Font;
        Categories = viewModel.SelectedGlyphCategories.ToList(); // Makes a copy of the list
        Variant = viewModel.RenderingOptions.Variant;
        Options = viewModel.RenderingOptions;
        GlyphSize = viewModel.Settings.PngSize;
        ExportColor = viewModel.ShowColorGlyphs;

        IsWhiteChecked = ResourceHelper.GetEffectiveTheme() == ElementTheme.Dark;
        IsBlackChecked = ResourceHelper.GetEffectiveTheme() == ElementTheme.Light;

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        UpdateCharacters();
    }

    protected override void OnPropertyChangeNotified(string property)
    {
        switch (property)
        {
            case nameof(GlyphColor):
                if (GlyphColor != Colors.Black)
                    IsBlackChecked = false;
                if (GlyphColor != Colors.White)
                    IsWhiteChecked = false;
                break;

            case nameof(IsWhiteChecked) when IsWhiteChecked:
                GlyphColor = Colors.White;
                break;

            case nameof(IsBlackChecked) when IsBlackChecked:
                GlyphColor = Colors.Black;
                break;

            case nameof(HideWhitespace):
                UpdateCharacters();
                break;

            case nameof(SelectedFormat):
                OnPropertyChanged(nameof(IsPngFormat));
                UpdateSummary();
                break;

            case nameof(Characters):
                UpdateSummary();
                OnPropertyChanged(nameof(CanContinue));
                break;
        }
    }

    private void UpdateCharacters()
    {
        // Fast path : all characters;
        if (!Categories.Any(c => !c.IsSelected) && !HideWhitespace)
        {
            Characters = Variant.Characters;
            return;
        }

        // Filter characters
        Characters = Unicode.FilterCharacters(Variant.Characters, Categories, HideWhitespace);
    }

    public void UpdateCategories(IList<UnicodeRangeModel> value)
    {
        Set(value, nameof(Categories), false);
        UpdateCharacters();
        OnPropertyChanged(nameof(Categories));
    }

    public void UpdateSummary()
    {
        SelectedText = Localization.Get(
            "ExportGlyphSelectedCharacters/Text",
            Characters.Count,
            Variant.Characters.Count);

        Summary = Localization.Get(
            "ExportGlyphsSummary/Text",
            Characters.Count,
            ((ExportFormat)SelectedFormat).ToString().ToUpper());
    }

    public async void StartExport()
    {
        ExportOptions export = new(
            (ExportFormat)SelectedFormat,
            ExportColor ? ExportStyle.ColorGlyph : ExportStyle.Black)
        {
            FileNameTemplate = GlyphNameModel.Template,
            PreferredColor = GlyphColor,
            PreferredSize = GlyphSize,
            SkipEmptyGlyphs = SkipBlankGlyphs,
            Font = Font,
            Options = Options
        };

        IsExporting = true;

        _currentToken = new CancellationTokenSource();

        int exported = 0;
        ExportGlyphsResult result = await ExportManager.ExportGlyphsToFolderAsync(
            Characters, export, (index, count) =>
        {
            exported = index;
            _dispatcherQueue.TryEnqueue(() =>
            {
                ShowCancelExport = true;
                ExportMessage = Localization.Get("ExportGlyphsProgressMessage/Text", index, count);
            });
        }, _currentToken.Token);

        if (result is not null)
        {
            int count = _currentToken.IsCancellationRequested ? exported - 1 : exported;
            WeakReferenceMessenger.Default.Send(
               new AppNotificationMessage(true, result));
        }

        ExportMessage = "";
        ShowCancelExport = false;
        IsExporting = false;
    }

    public void CancelExport()
    {
        _currentToken?.Cancel();
    }

    public void ToggleTheme()
    {
        PreviewTheme = PreviewTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
    }
}
