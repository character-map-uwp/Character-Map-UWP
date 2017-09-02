using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using CharacterMap.ViewModels;

namespace CharacterMap.Views
{
    public sealed partial class SettingsPage : Page
    {
        private SettingsViewModel ViewModel => DataContext as SettingsViewModel;

        private CoreApplicationViewTitleBar _coreTitleBar = CoreApplication.GetCurrentView().TitleBar;


        public SettingsPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _coreTitleBar.ExtendViewIntoTitleBar = true;
            Window.Current.SetTitleBar(TitleBarBackgroundElement);
            ViewModel.Initialize();
        }
    }
}
