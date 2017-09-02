using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Views;

namespace CharacterMap.Services
{
    public class WhatsNewDisplayService
    {
        internal static async Task ShowIfAppropriateAsync()
        {
            var currentVersion = PackageVersionToReadableString(Package.Current.Id.Version);

            var lastVersion = await Windows.Storage.ApplicationData.Current.LocalSettings.ReadAsync<string>(nameof(currentVersion));

            if (lastVersion == null)
            {
                await Windows.Storage.ApplicationData.Current.LocalSettings.SaveAsync(nameof(currentVersion), currentVersion);
            }
            else
            {
                if (currentVersion != lastVersion)
                {
                    await Windows.Storage.ApplicationData.Current.LocalSettings.SaveAsync(nameof(currentVersion), currentVersion);

                    var dialog = new WhatsNewDialog();
                    await dialog.ShowAsync();
                }
            }
        }

        private static string PackageVersionToReadableString(PackageVersion packageVersion)
        {
            return $"{packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}.{packageVersion.Revision}";
        }
    }
}
