using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

[DependencyProperty<string>("Definitions")]
[DependencyProperty<Orientation>("Orientation", Orientation.Vertical, nameof(InvalidateArrange))]
public partial class AutoGrid : Grid
{
    partial void OnDefinitionsChanged(string o, string n) => Properties.SetGridDefinitions(this, n);

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Orientation is Orientation.Vertical)
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

                if (child.ReadLocalValue(Grid.RowProperty) == DependencyProperty.UnsetValue)
                    Grid.SetRow(child, row);
            }
        }
        else
        {
            var columns = this.Children.OfType<FrameworkElement>().Count(c => Grid.GetRow(c) == 0);
            while (this.ColumnDefinitions.Count > columns)
                this.ColumnDefinitions.RemoveAt(0);
            while (this.ColumnDefinitions.Count < columns)
                this.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int col = -1;
            foreach (var child in this.Children.OfType<FrameworkElement>())
            {
                if (Grid.GetRow(child) == 0)
                    col++;

                if (child.ReadLocalValue(Grid.ColumnProperty) == DependencyProperty.UnsetValue)
                    Grid.SetColumn(child, col);
            }
        }

        return base.ArrangeOverride(finalSize);
    }
}
