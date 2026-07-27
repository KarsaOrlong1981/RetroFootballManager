using System.Globalization;
using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Converters
{
    // Border color for a pitch token based on how well the player fits the slot:
    // green = home position, orange = listed secondary position, red = out of position.
    public class PositionFitColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is PositionFitLevel fit
                ? fit switch
                {
                    PositionFitLevel.Favorite => Color.FromArgb("#22C55E"),
                    PositionFitLevel.Secondary => Color.FromArgb("#F59E0B"),
                    PositionFitLevel.OutOfPosition => Color.FromArgb("#EF4444"),
                    _ => Color.FromArgb("#334155"),
                }
                : Color.FromArgb("#334155");

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
