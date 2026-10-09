namespace CharacterMap.Models;

/// <summary>
/// Set an ItemsSource FOLLOWED by a SelectedItem to ensure the selection gets set properly, otherwise the
/// binding engine can set them in a different order and the selection will not be set correctly.
/// </summary>
public class ItemsSelectionModel
{
    public object ItemsSource { get; set; }
    public object SelectedItem { get; set; }
}