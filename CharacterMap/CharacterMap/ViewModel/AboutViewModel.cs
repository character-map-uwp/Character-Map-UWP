using System;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace CharacterMap.ViewModel
{
    public class AboutViewModel : ViewModelBase
    {
        public string DisplayName => Edi.UWP.Helpers.Utils.GetAppDisplayName();

        public string Publisher => Edi.UWP.Helpers.Utils.GetAppPublisher();

        public string Version => Edi.UWP.Helpers.Utils.GetAppVersion();

        public string Architecture => Edi.UWP.Helpers.Utils.Architecture;

        public RelayCommand CommandReview { get; set; }

        public AboutViewModel()
        {
            CommandReview = new RelayCommand(async () => await Edi.UWP.Helpers.Tasks.OpenStoreReviewAsync());
        }
    }
}
