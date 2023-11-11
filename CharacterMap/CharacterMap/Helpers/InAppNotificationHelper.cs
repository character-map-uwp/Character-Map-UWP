using Microsoft.Toolkit.Uwp.UI.Controls;
using Windows.UI.Xaml;

namespace CharacterMap.Helpers;

public interface IInAppNotificationPresenter
{
    InAppNotification GetNotifier();
}

public static class InAppNotificationHelper
{
    public static void OnMessage<T>(T presenter, AppNotificationMessage msg)
        where T : FrameworkElement, IInAppNotificationPresenter
    {
        if (msg.Local && !presenter.Dispatcher.HasThreadAccess)
            return;

        if (msg.Data is ExportResult result)
        {
            if (result.State is not ExportState.Succeeded)
                return;

            var content = ResourceHelper.InflateDataTemplate("ExportNotificationTemplate", result);
            ShowNotification(presenter, content, 5000);
        }
        else if (msg.Data is ExportGlyphsResult glyphsResult)
        {
            if (!glyphsResult.Success)
                return;

            var content = ResourceHelper.InflateDataTemplate("ExportGlyphsTemplate", glyphsResult);
            ShowNotification(presenter, content, 5000);
        }
        else if (msg.Data is ExportFontFileResult fontExportResult)
        {
            if (!fontExportResult.Success)
                return;

            var content = ResourceHelper.InflateDataTemplate("ExportFontNotificationTemplate", fontExportResult);
            ShowNotification(presenter, content, 5000);
        }
        else if (msg.Data is AddToCollectionResult added)
        {
            if (!added.Success)
                return;

            var content = ResourceHelper.InflateDataTemplate("AddedToCollectionNotificationTemplate", added);
            ShowNotification(presenter, content, 5000);
        }
        else if (msg.Data is CollectionUpdatedArgs cua)
        {
            var content = ResourceHelper.InflateDataTemplate("RemoveFromCollectionNotification", cua);
            ShowNotification(presenter, content, 5000);
        }
        else if (msg.Data is string s)
        {
            ShowNotification(presenter, s, msg.DurationInMilliseconds > 0 ? msg.DurationInMilliseconds : 4000);
        }
    }

    public static void ShowNotification<T>(T presenter, object o, int durationMs)
        where T : FrameworkElement, IInAppNotificationPresenter
    {
        var notifier = presenter.GetNotifier();

        if (o is string s)
            notifier.Show(s, durationMs);
        else if (o is UIElement e)
            notifier.Show(e, durationMs);
    }
}
