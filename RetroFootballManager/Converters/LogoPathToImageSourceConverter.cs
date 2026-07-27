using System.Globalization;

namespace RetroFootballManager.Converters
{
    // Team.LogoPath (filename under Resources/Images/Logos/) -> ImageSource, or null
    // when no crest is set (UI then shows the abbreviation placeholder).
    public class LogoPathToImageSourceConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is string path && !string.IsNullOrWhiteSpace(path) ? ImageSource.FromFile(path) : null;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
