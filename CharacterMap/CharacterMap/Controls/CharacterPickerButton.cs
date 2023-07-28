using CharacterMap.Models;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls
{
    public sealed class CharacterPickerButton : ContentControl
    {
        private static PropertyMetadata NULL_META = new (null);


        public FlyoutPlacementMode Placement
        {
            get { return (FlyoutPlacementMode)GetValue(PlacementProperty); }
            set { SetValue(PlacementProperty, value); }
        }

        public static readonly DependencyProperty PlacementProperty =
            DependencyProperty.Register(nameof(Placement), typeof(FlyoutPlacementMode), typeof(CharacterPickerButton), new PropertyMetadata(FlyoutPlacementMode.Bottom, (d,e) =>
            {
                ((CharacterPickerButton)d).UpdatePlacement();
            }));

        public event EventHandler<Character> CharacterSelected;

        public Control Target
        {
            get { return (Control)GetValue(TargetProperty); }
            set { SetValue(TargetProperty, value); }
        }

        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register(nameof(Target), typeof(Control), typeof(CharacterPickerButton), NULL_META);

        public CharacterRenderingOptions Options
        {
            get { return (CharacterRenderingOptions)GetValue(OptionsProperty); }
            set { SetValue(OptionsProperty, value); }
        }

        public static readonly DependencyProperty OptionsProperty =
            DependencyProperty.Register(nameof(Options), typeof(CharacterRenderingOptions), typeof(CharacterPickerButton), NULL_META);

        public CharacterPickerButton() 
        {
            this.DefaultStyleKey = typeof(CharacterPickerButton);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (this.GetTemplateChild("RootButton") is Button b)
            {
                if (b.Flyout is { } flyout)
                {
                    flyout.Opening -= Flyout_Opening;
                    flyout.Opening += Flyout_Opening;
                }
            }

            UpdatePlacement();
        }

        void UpdatePlacement()
        {
            VisualStateManager.GoToState(this, Placement == FlyoutPlacementMode.BottomEdgeAlignedRight ? "RightPlacementState" : "DefaultPlacementState", false);
        }

        private void Flyout_Opening(object sender, object e)
        {
            if (sender is Flyout { } flyout)
            {
                if (flyout.Content is CharacterPicker p)
                    p.CharacterSelected -= Picker_CharacterSelected;

                p = new CharacterPicker(flyout, Options);
                p.CharacterSelected += Picker_CharacterSelected;
                flyout.Content = p;
            }
        }

        private void Picker_CharacterSelected(object sender, Character e)
        {
            if (Target is AutoSuggestBox ab)
            {
                if (ab.GetFirstDescendantOfType<TextBox>() is { } t && t.SelectionStart > -1)
                {
                    string txt = ab.Text;
                    int start = t.SelectionStart;
                    if (t.SelectionLength > 0)
                        txt = txt.Remove(t.SelectionStart, t.SelectionLength);

                    ab.Text = txt.Insert(t.SelectionStart, e.Char);
                    t.SelectionStart = start + 1;
                }
            }
            if (Target is SuggestionBox sb)
                sb.Text += e.Char;
            else if (Target is TextBox box)
                box.Text += e.Char;
            else if (Target is ComboBox c)
                c.Text += e.Char;

            CharacterSelected?.Invoke(this, e);
        }
    }
}
