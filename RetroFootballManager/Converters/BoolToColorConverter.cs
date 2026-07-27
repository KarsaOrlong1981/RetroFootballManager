using System.Globalization;

namespace RetroFootballManager.Converters
{
    // true -> TrueColor, false -> FalseColor. Used e.g. to highlight an active toggle button.
    public class BoolToColorConverter : IValueConverter
    {
        public Color TrueColor { get; set; } = Colors.Transparent;
        public Color FalseColor { get; set; } = Colors.Transparent;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is bool b && b ? TrueColor : FalseColor;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
