using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;

[DependencyProperty("ItemsSource")]
[DependencyProperty<string>("Text", "string.Empty")]
[DependencyProperty<string>("PlaceholderText", "string.Empty")]
[DependencyProperty<bool>("IsCharacterPickerEnabled", true)]
[DependencyProperty<CharacterRenderingOptions>("RenderingOptions")]
[DependencyProperty<FlowDirection>("DetectedFlowDirection")]
public sealed partial class SuggestionBox : Control
{
    public event EventHandler<FlowDirection> DetectedFlowDirectionChanged;

    private AutoSuggestBox _suggestBox = null;

    private Button _suggestButton = null;

    private Throttler _scrollThrottle { get; } = new();

    private Debouncer _suggestionDebouncer { get; } = new();

    private bool block = false;
    private long _token = 0;

    public SuggestionBox()
    {
        this.DefaultStyleKey = typeof(SuggestionBox);
    }

    partial void OnIsCharacterPickerEnabledChanged(bool? oldValue, bool newValue) => UpdatePickerState(true);

    partial void OnDetectedFlowDirectionChanged(FlowDirection? oldValue, FlowDirection newValue)
    {
        DetectedFlowDirectionChanged?.Invoke(this, (FlowDirection)newValue);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // Unhook old things
        if (_suggestBox is not null)
        {
            _suggestBox.PointerWheelChanged -= _suggestBox_PointerWheelChanged;
            _suggestBox.TextChanged -= Box_TextChanged;
            _suggestBox.SizeChanged -= _suggestBox_SizeChanged;
            _suggestBox.GotFocus -= _suggestBox_GotFocus;
            _suggestBox.LostFocus -= _suggestBox_LostFocus;
            _suggestBox.KeyDown -= _suggestBox_KeyDown;
        }

        if (_suggestButton is not null)
            _suggestButton.Click -= _suggestButton_Click;


        if (this.ItemsSource is IEnumerable<Suggestion> sug)
        {
            Text = sug.FirstOrDefault()?.Text ?? string.Empty;
        }

        UpdatePickerState(false);

        // Hook new things
        _suggestBox = this.GetTemplateChild("SuggestionBox") as AutoSuggestBox;
        if (_suggestBox is not null)
        {
            _suggestBox.PointerWheelChanged += _suggestBox_PointerWheelChanged;
            _suggestBox.TextChanged += Box_TextChanged;
            _suggestBox.SizeChanged += _suggestBox_SizeChanged;
            _suggestBox.GotFocus += _suggestBox_GotFocus;
            _suggestBox.LostFocus += _suggestBox_LostFocus;
            _suggestBox.KeyDown += _suggestBox_KeyDown;

            if (_token != 0)
                _suggestBox.UnregisterPropertyChangedCallback(AutoSuggestBox.IsSuggestionListOpenProperty, _token);
        }

        // Try to find the "Edit Suggestions" button and hook up the click handler
        if (this.GetTemplateChild("EditButton") is ListViewItem edit)
        {
            edit.Tapped -= Item_Tapped;
            edit.Tapped += Item_Tapped;
        }

        _suggestButton = this.GetTemplateChild("SuggestionButton") as Button;
        if (_suggestButton is not null)
        {
            _token = _suggestBox.RegisterPropertyChangedCallback(AutoSuggestBox.IsSuggestionListOpenProperty, SuggestCallback);
            _suggestButton.Click += _suggestButton_Click;
        }
    }

    private void SuggestCallback(DependencyObject sender, DependencyProperty dp)
    {
        if (_suggestBox.IsSuggestionListOpen is false)
            VisualStateManager.GoToState(_suggestBox, "Closed", false);
        else
            VisualStateManager.GoToState(_suggestBox, "Opened", ResourceHelper.AllowAnimation);
    }

    void UpdatePickerState(bool animate)
    {
        VisualStateManager.GoToState(this, IsCharacterPickerEnabled ? "PickerVisibleState" : "PickerHiddenState", animate);
    }

    private void _suggestBox_GotFocus(object sender, RoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "Focused", true);
    }

    private void _suggestBox_LostFocus(object sender, RoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "NotFocused", true);
    }

    private void _suggestBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Up)
            Scroll(1);
        else if (e.Key == Windows.System.VirtualKey.Down)
            Scroll(-1);
    }

    private void _suggestBox_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _suggestBox.SizeChanged -= _suggestBox_SizeChanged;

        // List to TextBox to discern flow direction
        if (_suggestBox.GetFirstDescendantOfType<TextBox>() is TextBox t)
        {
            t.TextChanged -= T_TextChanged;
            t.TextChanged += T_TextChanged;
        }

        if (_suggestBox.GetFirstDescendantOfType<Popup>() is Popup p)
        {
            p.Opened -= P_Opened;
            p.Opened += P_Opened;
        }
    }

    private void Item_Tapped(object sender, TappedRoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new EditSuggestionsRequested());
    }

    private void T_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox box && box.Text is not null && box.Text.Length > 0)
            DetectedFlowDirection = Utils.GetFlowDirection(box);
    }

    private void P_Opened(object sender, object e)
    {
        if (sender is Popup p)
        {
            // Fix offset problems due to our global Transform3D
            p.Translation = new(0, 0, 0);

            // Force to be width of this entire control
            //if (p.Child is FrameworkElement f)
            //    f.Width = this.ActualWidth;
        }
    }


    private void _suggestBox_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        // Allows changing the selected text by using mouse wheel or touchpad
        _scrollThrottle.Throttle(150, () =>
        {
            var point = e.GetCurrentPoint(this);
            var props = point.Properties;
            if (props.IsHorizontalMouseWheel)
                return;

            var delta = props.MouseWheelDelta;
            if (delta > -2 && delta < 2)
                return;

            Scroll(delta);
        });
    }

    private void Scroll(int delta)
    {
        int idx = -1;
        if (ItemsSource is IReadOnlyList<Suggestion> items)
        {
            if (items.FirstOrDefault(i => i.Text == Text) is Suggestion match)
                idx = items.ToList().IndexOf(match);

            idx += delta < 0 ? 1 : -1;
            if (idx >= items.Count)
                idx = 0;
            else if (idx <= -1)
                idx = items.Count - 1;

            Text = items[idx].Text;
            _suggestBox.IsSuggestionListOpen = false;

            block = true;
        }
    }

    private void _suggestButton_Click(object sender, RoutedEventArgs e)
    {
        if (_suggestBox is null)
            return;

        _suggestBox.ItemsSource = ItemsSource;
        _suggestBox.IsSuggestionListOpen = true;
    }


    private void Box_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        // This handler updates the suggestions and shows the list, if appropriate

        // The PointerWheel handler can cause this handler to be called after updating the suggestions,
        // but we don't want to handle it here in that case.
        if (block)
        {
            block = false;
            return;
        }

        // Functions as auto-close for the suggestion list
        if (args.Reason is AutoSuggestionBoxTextChangeReason.SuggestionChosen or AutoSuggestionBoxTextChangeReason.ProgrammaticChange)
        {
            sender.IsSuggestionListOpen = false;
            return;
        }

        string text = sender.Text;
        _suggestionDebouncer.Debounce(64, () =>
        {
            if (args.CheckCurrent())
            {
                if (ItemsSource is IEnumerable<Suggestion> options)
                {
                    var items = options.Where(op => op.Text.Contains(text, StringComparison.InvariantCultureIgnoreCase)).ToList();
                    if (args.CheckCurrent())
                    {
                        if ((items.Count == 1 && items[0].Text == Text) is false)
                        {
                            sender.ItemsSource = items;
                            sender.IsSuggestionListOpen = true;
                        }
                    }
                }
            }
        });

    }
}
