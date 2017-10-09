using Windows.UI.Xaml.Controls;

namespace CharacterMap.UserControls
{
    public sealed partial class DPIIndicator : UserControl
    {
        public DPIIndicator()
        {
            this.InitializeComponent();
            TxtLocalDpi.Text = Windows.Graphics.Display.DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel.ToString("0%");
        }
    }
}
