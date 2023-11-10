using CharacterMap.Activation;
using Windows.ApplicationModel.Activation;
using Windows.UI.Notifications;

namespace CharacterMap.Services;

internal partial class ToastNotificationsService : ActivationHandler<ToastNotificationActivatedEventArgs>
{
    public void ShowToastNotification(ToastNotification toastNotification)
    {
        ToastNotificationManager.CreateToastNotifier().Show(toastNotification);
    }

    protected override async Task HandleInternalAsync(ToastNotificationActivatedEventArgs args)
    {
        await Task.CompletedTask;
    }
}
