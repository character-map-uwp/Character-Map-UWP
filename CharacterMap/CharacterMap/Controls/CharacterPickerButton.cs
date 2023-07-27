using CharacterMap.Models;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls
{
    public sealed class CharacterPickerButton : ContentControl
    {
        private static PropertyMetadata NULL_META = new PropertyMetadata(null);

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
            if (Target is TextBox box)
                box.Text += e.Char;
            else if (Target is ComboBox c)
                c.Text += e.Char;

            CharacterSelected?.Invoke(this, e);
        }
    }
}
