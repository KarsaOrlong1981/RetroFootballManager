using RetroFootballManager.Models;

namespace RetroFootballManager.ViewModels
{
    // Live/full-time match statistics for one team, shown in the Statistik dialog.
    public record MatchStatsDisplay(
        string TeamName,
        int Possession,
        int PassAccuracy,
        int Shots,
        int ShotsOnTarget,
        int Corners,
        int FreeKicks,
        int Penaltys,
        int Offsides,
        int Fouls,
        int YellowCards,
        int RedCards)
    {
        public static MatchStatsDisplay From(string teamName, MatchStats stats) => new(
            teamName,
            stats.Possession,
            stats.PassAccuracy,
            stats.Shots,
            stats.ShotsOnTarget,
            stats.Corners,
            stats.FreeKicks,
            stats.Penaltys,
            stats.Offsides,
            stats.Fouls,
            stats.YellowCards,
            stats.RedCards);
    }
}
