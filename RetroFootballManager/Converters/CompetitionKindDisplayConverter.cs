using System.Globalization;
using RetroFootballManager.Core.Models;

namespace RetroFootballManager.Converters
{
    public class CompetitionKindDisplayConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is CompetitionKind kind
                ? kind switch
                {
                    CompetitionKind.EuropeanCup => "Europa Pokal",
                    CompetitionKind.EuropeanMasterCup => "Europa Pokal der Meister",
                    CompetitionKind.GermanCup => "Deutscher Pokal",
                    CompetitionKind.Tier1 => "1.Liga",
                    CompetitionKind.Tier2 => "2.Liga",
                    CompetitionKind.Tier3 => "3.Liga",
                    CompetitionKind.Tier4 => "4.Liga",
                    _ => string.Empty
                }
                : string.Empty;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
