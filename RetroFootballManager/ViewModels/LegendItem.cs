using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public record LegendItem(Color Color, string Label);

    // Builds the color-block legend shown below a league table, matching whichever zones
    // actually apply to that tier (see LeagueZoneHelper).
    public static class LegendBuilder
    {
        public static List<LegendItem> BuildFor(int tier)
        {
            var items = new List<LegendItem>();

            if (tier == Common.SeasonProgressionService.TopTier)
            {
                items.Add(new LegendItem(Color.FromRgba(234, 179, 8, 90), "Europa Pokal der Meister"));
                items.Add(new LegendItem(Color.FromRgba(249, 115, 22, 90), "Europa-Pokal"));
            }
            else
            {
                items.Add(new LegendItem(Color.FromRgba(34, 197, 94, 90), "Aufstieg"));
            }

            if (tier < Common.SeasonProgressionService.BottomTier)
                items.Add(new LegendItem(Color.FromRgba(220, 38, 38, 90), "Abstieg"));

            return items;
        }
    }
}
