using System.Globalization;

namespace RetroFootballManager.Converters
{
    // values[0] == values[1] -> TrueColor, else FalseColor. Used to highlight the selected row
    // in a BindableLayout list (no built-in SelectedItem/selection styling like CollectionView has).
    public class EqualsToColorConverter : IMultiValueConverter
    {
        public Color TrueColor { get; set; } = Colors.Transparent;
        public Color FalseColor { get; set; } = Colors.Transparent;

        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
            values is [{ } a, { } b] && Equals(a, b) ? TrueColor : FalseColor;

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
