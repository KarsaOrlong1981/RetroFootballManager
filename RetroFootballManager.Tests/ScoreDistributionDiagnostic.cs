using RetroFootballManager.Common;
using Xunit;

namespace RetroFootballManager.Tests
{
    // One-off investigation (not a regression test): simulates 10 matchdays per league tier
    // using the real UniverseGenerator squads and writes a score-distribution report to
    // %TEMP%\rfm_score_distribution.txt, to check whether low-scoring, narrow-margin games
    // (reported for Tier 4) also occur in Tier 1-3.
    public class ScoreDistributionDiagnostic
    {
        [Fact]
        public void SimulateTenMatchdaysPerTier_WritesScoreDistributionReport()
        {
            var rng = new Random(42);
            var (_, teams) = UniverseGenerator.CreateUniverse(season: 1, random: rng);

            int nextId = 1;
            foreach (var team in teams)
                team.Id = nextId++;

            var start = FixtureGenerator.FirstSaturdayOnOrAfter(new DateTime(2026, 8, 1));
            var report = new System.Text.StringBuilder();

            foreach (var tier in Enumerable.Range(1, 4))
            {
                var tierTeams = teams.Where(t => t.LeagueTier == tier).ToList();
                var teamIds = tierTeams.Select(t => t.Id).ToList();
                var teamsById = tierTeams.ToDictionary(t => t.Id);

                var fixtures = FixtureGenerator.GenerateLeagueFixtures(teamIds, season: 1, leagueTier: tier, start);

                var games = new List<(int Home, int Away)>();
                foreach (var fixture in fixtures.Where(f => f.Matchday <= 10))
                {
                    var home = teamsById[fixture.HomeTeamId];
                    var away = teamsById[fixture.AwayTeamId];
                    MatchDayService.PrepareForMatch(home);
                    MatchDayService.PrepareForMatch(away);
                    var match = new Match(home, away, rng) { HomeCoach = new AiMatchCoach(), AwayCoach = new AiMatchCoach() };
                    var result = match.Simulate();
                    games.Add((result.HomeGoals, result.AwayGoals));
                }

                var avgRating = tierTeams.SelectMany(t => t.Players).Average(p => p.Rating);
                report.AppendLine($"=== Liga {tier} ({games.Count} Spiele, {tierTeams.Count} Teams, Ø Spielerrating {avgRating:0.0}) ===");
                report.AppendLine($"Ø Tore Heim: {games.Average(g => g.Home):0.00}  Ø Tore Auswärts: {games.Average(g => g.Away):0.00}  Ø Tore gesamt: {games.Average(g => g.Home + g.Away):0.00}");
                report.AppendLine($"0:0-Anteil: {games.Count(g => g is (0, 0)) * 100.0 / games.Count:0.0}%  Max. Torabstand: {games.Max(g => Math.Abs(g.Home - g.Away))}  Spiele mit >=3 Toren einer Seite: {games.Count(g => g.Home >= 3 || g.Away >= 3)}");

                foreach (var grp in games.GroupBy(g => $"{g.Home}:{g.Away}").OrderByDescending(g => g.Count()))
                    report.AppendLine($"  {grp.Key}: {grp.Count()} ({grp.Count() * 100.0 / games.Count:0.0}%)");

                report.AppendLine();
            }

            var path = Path.Combine(Path.GetTempPath(), "rfm_score_distribution.txt");
            File.WriteAllText(path, report.ToString());
            Assert.True(File.Exists(path));
        }
    }
}
