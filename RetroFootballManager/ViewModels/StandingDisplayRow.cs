using RetroFootballManager.Common;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public record FormBadge(char Letter, Color Color);

    // Wraps a Core StandingRow with UI-only presentation info (zone color, form badges)
    // so the table view can render relegation/promotion/European-spot highlighting.
    public record StandingDisplayRow(StandingRow Row, LeagueZone Zone)
    {
        public Color RowColor => Zone switch
        {
            LeagueZone.Relegation => Color.FromRgba(220, 38, 38, 60),       // transparent red
            LeagueZone.Promotion => Color.FromRgba(34, 197, 94, 55),        // transparent green
            LeagueZone.ChampionsLeague => Color.FromRgba(234, 179, 8, 60),  // transparent yellow
            LeagueZone.EuropaLeague => Color.FromRgba(249, 115, 22, 55),   // transparent orange
            _ => Colors.Transparent,
        };

        public List<FormBadge> FormBadges => Row.Form
            .Select(c => new FormBadge(c, c switch
            {
                'W' => Color.FromArgb("#22C55E"),
                'L' => Color.FromArgb("#EF4444"),
                _ => Color.FromArgb("#9CA3AF"),
            }))
            .ToList();
    }
}
