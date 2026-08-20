using System.Globalization;

namespace WorkHub.Converters;

// values: [0] = the row's item (Binding Path="."), [1] = the owning
// CollectionView's SelectedItem. Reference equality is correct here: list
// merges re-point selection at the instance living in the bound collection.
public class IsSelectedItemConverter : IMultiValueConverter
{
    public object Convert(object[]? values, Type targetType, object? parameter, CultureInfo culture) =>
        values is { Length: 2 } && values[0] != null && ReferenceEquals(values[0], values[1]);

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
