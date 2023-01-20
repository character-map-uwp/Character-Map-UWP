using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace CharacterMap.Controls
{
    public sealed partial class CharacterPicker : UserControl
    {
        public event EventHandler<Character> CharacterSelected;

        public CharacterRenderingOptions Options
        {
            get { return (CharacterRenderingOptions)GetValue(OptionsProperty); }
            set { SetValue(OptionsProperty, value); }
        }

        public static readonly DependencyProperty OptionsProperty =
            DependencyProperty.Register(nameof(Options), typeof(CharacterRenderingOptions), typeof(CharacterPicker), new PropertyMetadata(0));

        private Flyout _parent = null;

        public CharacterPicker()
        {
            this.InitializeComponent();
        }

        public CharacterPicker(Flyout parent, CharacterRenderingOptions options) : this()
        {
            _parent = parent;
            Options = options;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsGridView.SelectedItem is Character c)
                CharacterSelected?.Invoke(this, c);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (_parent is not null)
            {
                _parent.Hide();
                _parent = null;
            }
        }
    }
}
