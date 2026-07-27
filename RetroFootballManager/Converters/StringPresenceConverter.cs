using System.Globalization;

namespace RetroFootballManager.Converters
{
    // Whether a string is set - toggles between crest image and abbreviation fallback
    // (Invert=true for the fallback label, which should only show when there's no crest).
    public class StringPresenceConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool hasValue = value is string s && !string.IsNullOrWhiteSpace(s);
            return Invert ? !hasValue : hasValue;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
