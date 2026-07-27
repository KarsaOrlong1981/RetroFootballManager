using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public static class TrophyMapping
    {
        public static TrophyType FromLeagueTier(int tier) => tier switch
        {
            1 => TrophyType.DeutscherMeister,
            2 => TrophyType.MeisterLiga2,
            3 => TrophyType.MeisterLiga3,
            4 => TrophyType.MeisterLiga4,
            _ => throw new ArgumentOutOfRangeException(nameof(tier)),
        };

        public static TrophyType FromCompetition(CompetitionType competition) => competition switch
        {
            CompetitionType.GermanCup => TrophyType.DeutscherPokal,
            CompetitionType.ChampionsLeague => TrophyType.EuropaPokalDerMeister,
            CompetitionType.EuropaCup => TrophyType.EuropaPokal,
            _ => throw new ArgumentOutOfRangeException(nameof(competition)),
        };
    }
}
