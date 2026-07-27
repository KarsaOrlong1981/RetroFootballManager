using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public record LeagueTableResult(
        int Tier,
        List<StandingRow> Table,
        List<int> PromotedTeamIds,
        List<int> RelegatedTeamIds);

    public record SeasonEndResult(
        int Season,
        List<LeagueTableResult> Leagues,
        int ManagerFinalPosition,
        int ManagerTier,
        int PointsAwarded,
        string ManagerOutcome,
        bool ManagerPromoted);

    // Evaluates the end of season: tables per league, promotion/relegation (top 3 up, bottom 3
    // down; league 4 has no relegation) and awards global career points based on how the
    // player-managed team performed. Mutates Team.LeagueTier for the following season.
    public static class SeasonProgressionService
    {
        public const int PromotionSpots = 3;
        public const int RelegationSpots = 3;
        public const int TopTier = 1;
        public const int BottomTier = 4;

        // Point awards (initial values, easy to tune).
        private const int SeasonCompletedPoints = 25;
        private const int ChampionPoints = 50;
        private const int PromotionPoints = 100;
        private const int TopSixPoints = 20;
        private const int SurvivalPoints = 10;

        public static SeasonEndResult EndSeason(
            int season,
            IReadOnlyList<Team> teams,
            IReadOnlyList<Fixture> fixtures,
            int managerTeamId,
            CareerService career)
        {
            var names = teams.ToDictionary(t => t.Id, t => t.Name);
            var leagues = new List<LeagueTableResult>();

            var tiers = fixtures.Select(f => f.LeagueTier).Distinct().OrderBy(t => t).ToList();
            foreach (var tier in tiers)
            {
                var tierFixtures = fixtures.Where(f => f.LeagueTier == tier).ToList();
                var table = StandingsCalculator.Calculate(tierFixtures, names);

                var promoted = tier > TopTier
                    ? table.Take(PromotionSpots).Select(r => r.TeamId).ToList()
                    : [];
                var relegated = tier < BottomTier
                    ? table.TakeLast(RelegationSpots).Select(r => r.TeamId).ToList()
                    : [];

                leagues.Add(new LeagueTableResult(tier, table, promoted, relegated));
            }

            // Determine the manager's performance BEFORE league membership is changed.
            var managerTeam = teams.FirstOrDefault(t => t.Id == managerTeamId);
            int managerTier = managerTeam?.LeagueTier ?? BottomTier;
            var managerLeague = leagues.FirstOrDefault(l => l.Tier == managerTier);
            var managerRow = managerLeague?.Table.FirstOrDefault(r => r.TeamId == managerTeamId);
            int managerPos = managerRow?.Position ?? 0;

            bool promotedManager = managerLeague?.PromotedTeamIds.Contains(managerTeamId) ?? false;
            bool relegatedManager = managerLeague?.RelegatedTeamIds.Contains(managerTeamId) ?? false;

            var (points, outcome) = ComputeManagerPoints(managerPos, managerTier, promotedManager, relegatedManager);
            if (points > 0)
                career.AwardPoints(season, outcome, points);

            // Apply promotion/relegation (set team tiers for the next season).
            foreach (var league in leagues)
            {
                foreach (var id in league.PromotedTeamIds)
                    SetTier(teams, id, league.Tier - 1);
                foreach (var id in league.RelegatedTeamIds)
                    SetTier(teams, id, league.Tier + 1);
            }

            return new SeasonEndResult(season, leagues, managerPos, managerTier, points, outcome, promotedManager);
        }

        private static (int Points, string Outcome) ComputeManagerPoints(
            int position, int tier, bool promoted, bool relegated)
        {
            if (position == 0)
                return (0, "Keine Wertung");

            int points = SeasonCompletedPoints;
            var parts = new List<string> { "Saison beendet" };

            if (position == 1)
            {
                points += ChampionPoints;
                parts.Add("Meister");
            }

            if (promoted)
            {
                points += PromotionPoints;
                parts.Add("Aufstieg");
            }
            else if (position <= 6)
            {
                points += TopSixPoints;
                parts.Add("Top 6");
            }
            else if (!relegated)
            {
                points += SurvivalPoints;
                parts.Add("Klassenerhalt");
            }

            if (relegated)
                parts.Add("Abstieg");

            return (points, $"Liga {tier}, Platz {position}: {string.Join(", ", parts)}");
        }

        private static void SetTier(IReadOnlyList<Team> teams, int teamId, int newTier)
        {
            var team = teams.FirstOrDefault(t => t.Id == teamId);
            if (team is not null)
                team.LeagueTier = Math.Clamp(newTier, TopTier, BottomTier);
        }

        // Builds the fixture list for the next season from the leagues reassembled after
        // promotion/relegation.
        public static List<Fixture> BuildNextSeasonFixtures(
            IReadOnlyList<Team> teams,
            int newSeason,
            DateTime seasonStart)
        {
            var firstSaturday = FixtureGenerator.FirstSaturdayOnOrAfter(seasonStart);
            var all = new List<Fixture>();
            foreach (var tier in teams.Select(t => t.LeagueTier).Distinct().OrderBy(t => t))
            {
                var ids = teams.Where(t => t.LeagueTier == tier).Select(t => t.Id).ToList();
                all.AddRange(FixtureGenerator.GenerateLeagueFixtures(ids, newSeason, tier, firstSaturday));
            }
            return all;
        }
    }
}
