using RetroFootballManager.Common;
using Xunit;

namespace RetroFootballManager.Tests
{
    // Guards the match engine's real-world realism targets (see 2026-09-06 calibration pass):
    // goals, cards, offsides and result-distribution per game should land close to actual
    // professional football averages. Simulates a full season across all 4 tiers (1224
    // matches) for a stable sample; tolerance bands allow for run-to-run RNG variance while
    // still catching a real calibration regression.
    public class RealismCalibrationTests
    {
        private record GameStats(
            int HomeGoals, int AwayGoals, int Yellow, int Red, int Offsides, int Penaltys, int Fouls);

        [Fact]
        public void SimulateFullSeason_MatchesRealWorldAverages()
        {
            var rng = new Random(42);
            var (_, teams) = UniverseGenerator.CreateUniverse(season: 1, random: rng);

            int nextTeamId = 1;
            foreach (var team in teams)
                team.Id = nextTeamId++;

            // Real games always get unique player IDs via EF Core when a save is created
            // (SaveGameService.StartNewCareerAsync). Without this, every freshly generated
            // player defaults to Id 0, and per-player match stats (e.g. "does this fouler
            // already have a yellow?") collapse onto a single shared slot for all 22 players -
            // which silently corrupted this exact calibration during the pass above.
            int nextPlayerId = 1;
            foreach (var player in teams.SelectMany(t => t.Players.Concat(t.YouthPlayers)))
                player.Id = nextPlayerId++;

            var start = FixtureGenerator.FirstSaturdayOnOrAfter(new DateTime(2026, 8, 1));
            var games = new List<GameStats>();

            foreach (var tier in Enumerable.Range(1, 4))
            {
                var tierTeams = teams.Where(t => t.LeagueTier == tier).ToList();
                var teamIds = tierTeams.Select(t => t.Id).ToList();
                var teamsById = tierTeams.ToDictionary(t => t.Id);
                var fixtures = FixtureGenerator.GenerateLeagueFixtures(teamIds, season: 1, leagueTier: tier, start);

                foreach (var fixture in fixtures)
                {
                    var home = teamsById[fixture.HomeTeamId];
                    var away = teamsById[fixture.AwayTeamId];
                    MatchDayService.PrepareForMatch(home);
                    MatchDayService.PrepareForMatch(away);
                    var match = new Match(home, away, rng) { HomeCoach = new AiMatchCoach(), AwayCoach = new AiMatchCoach() };
                    var result = match.Simulate();
                    games.Add(new GameStats(
                        result.HomeGoals, result.AwayGoals,
                        result.MatchStatsHome.YellowCards + result.MatchStatsAway.YellowCards,
                        result.MatchStatsHome.RedCards + result.MatchStatsAway.RedCards,
                        result.MatchStatsHome.Offsides + result.MatchStatsAway.Offsides,
                        result.MatchStatsHome.Penaltys + result.MatchStatsAway.Penaltys,
                        result.MatchStatsHome.Fouls + result.MatchStatsAway.Fouls));
                }
            }

            double avgGoals = games.Average(g => g.HomeGoals + g.AwayGoals);
            double avgYellow = games.Average(g => g.Yellow);
            double avgRed = games.Average(g => g.Red);
            double avgOffsides = games.Average(g => g.Offsides);
            double avgPenaltys = games.Average(g => g.Penaltys);
            double goallessPercent = games.Count(g => g.HomeGoals == 0 && g.AwayGoals == 0) * 100.0 / games.Count;
            double drawPercent = games.Count(g => g.HomeGoals == g.AwayGoals) * 100.0 / games.Count;

            // Real-world reference averages (approximate, professional football): goals
            // 3.2/game, yellow cards 3.7/game, red cards 0.12/game, penalties 0.36/game (109
            // in 306 real Bundesliga games - a full-season sample, not the noisier 0.10/game
            // seen over an early 10-game stretch), goalless 5%, draws 24%. Offside has no
            // single authoritative reference value; ~6/game (~3/team) is a reasonable
            // approximation used here.
            Assert.InRange(avgGoals, 2.7, 3.7);
            Assert.InRange(avgYellow, 3.0, 4.4);
            Assert.InRange(avgRed, 0.05, 0.22);
            Assert.InRange(avgOffsides, 4.5, 7.5);
            Assert.InRange(avgPenaltys, 0.25, 0.47);
            Assert.InRange(goallessPercent, 3.0, 10.0);
            Assert.InRange(drawPercent, 19.0, 29.0);
        }
    }
}
