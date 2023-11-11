using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

public class AutoGrid : Grid
{
    protected override Size ArrangeOverride(Size finalSize)
    {
        var rows = this.Children.OfType<FrameworkElement>().Count(c => Grid.GetColumn(c) == 0);
        while (this.RowDefinitions.Count > rows)
            this.RowDefinitions.RemoveAt(0);
        while (this.RowDefinitions.Count < rows)
            this.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        int row = -1;
        foreach (var child in this.Children.OfType<FrameworkElement>())
        {
            if (Grid.GetColumn(child) == 0)
                row++;

            Grid.SetRow(child, row);
        }

        return base.ArrangeOverride(finalSize);
    }
}
