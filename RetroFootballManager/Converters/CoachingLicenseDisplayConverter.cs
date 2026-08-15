using System.Globalization;
using RetroFootballManager.Core.Models;
using RetroFootballManager.Models;

namespace RetroFootballManager.Converters
{
    public class CoachingLicenseDisplayConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is CoachingLicense license ? CoachingLicenseDisplay.Label(license) : string.Empty;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
