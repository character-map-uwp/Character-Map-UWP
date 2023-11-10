namespace CharacterMap.Services;

public interface IDialogService
{
    void ShowMessageBox(string message, string title);
    Task ShowMessageAsync(string message, string title);
}
