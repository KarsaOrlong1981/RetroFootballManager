using System.Globalization;
using RetroFootballManager.Core.Models;

namespace RetroFootballManager.Converters
{
    public class GroupConditionTypeDisplayConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is GroupConditionType type
                ? type switch
                {
                    GroupConditionType.Rating => "Rating",
                    GroupConditionType.Moral => "Moral",
                    _ => string.Empty
                }
                : string.Empty;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
