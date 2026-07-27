using RetroFootballManager.Common;

namespace RetroFootballManager.Services
{
    public enum LeagueZone { None, Relegation, Promotion, ChampionsLeague, EuropaLeague }

    // Determines which colored zone a table position falls into, mirroring
    // SeasonProgressionService's promotion/relegation rules (top3 up / bottom3 down,
    // Tier 1 has no promotion, Tier 4 has no relegation) plus the European spots for Tier 1.
    public static class LeagueZoneHelper
    {
        private const int ChampionsLeagueSpots = 4;
        private const int EuropaLeagueSpots = 3; // places 5-7

        public static LeagueZone GetZone(int tier, int position, int totalTeams)
        {
            if (tier == SeasonProgressionService.TopTier)
            {
                if (position <= ChampionsLeagueSpots)
                    return LeagueZone.ChampionsLeague;
                if (position <= ChampionsLeagueSpots + EuropaLeagueSpots)
                    return LeagueZone.EuropaLeague;
            }
            else if (position <= SeasonProgressionService.PromotionSpots)
            {
                return LeagueZone.Promotion;
            }

            if (tier < SeasonProgressionService.BottomTier &&
                position > totalTeams - SeasonProgressionService.RelegationSpots)
            {
                return LeagueZone.Relegation;
            }

            return LeagueZone.None;
        }
    }
}
