using System.Globalization;
using RetroFootballManager.Common;
using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Converters
{
    // Turns a formation slot's normalised (X,Y) into a proportional AbsoluteLayout Rect,
    // used for mini-pitch thumbnails and the large formation preview.
    public class FormationSlotBoundsConverter : IValueConverter
    {
        public double DotSize { get; set; } = 10;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is FormationSlot slot)
                return new Rect(slot.X, slot.Y, DotSize, DotSize);
            return new Rect(0, 0, DotSize, DotSize);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}

namespace RetroFootballManager.Converters
{
    // Turns a pitch token's normalised (X,Y) into a proportional AbsoluteLayout Rect.
    // Used with AbsoluteLayout.LayoutFlags="PositionProportional" (size stays absolute).
    public class PitchBoundsConverter : IValueConverter
    {
        public double TokenWidth { get; set; } = 150;
        public double TokenHeight { get; set; } = 104;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is PitchToken token)
                return new Rect(token.X, token.Y, TokenWidth, TokenHeight);
            return new Rect(0, 0, TokenWidth, TokenHeight);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
