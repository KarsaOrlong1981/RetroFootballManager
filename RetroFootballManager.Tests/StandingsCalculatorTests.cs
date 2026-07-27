using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class StandingsCalculatorTests
    {
        private static readonly Dictionary<int, string> Names = new()
        {
            [1] = "Alpha", [2] = "Beta", [3] = "Gamma",
        };

        [Fact]
        public void OrdersByPointsThenGoalDifference()
        {
            var fixtures = new List<Fixture>
            {
                // Alpha beats Beta 3:0
                new() { HomeTeamId = 1, AwayTeamId = 2, Played = true, HomeGoals = 3, AwayGoals = 0 },
                // Gamma beats Alpha 1:0
                new() { HomeTeamId = 3, AwayTeamId = 1, Played = true, HomeGoals = 1, AwayGoals = 0 },
                // Beta draws Gamma 2:2
                new() { HomeTeamId = 2, AwayTeamId = 3, Played = true, HomeGoals = 2, AwayGoals = 2 },
            };

            var table = StandingsCalculator.Calculate(fixtures, Names);

            // Alpha 3pts (+2), Gamma 4pts (+1), Beta 1pt (-3) → Gamma, Alpha, Beta
            Assert.Equal("Gamma", table[0].TeamName);
            Assert.Equal(4, table[0].Points);
            Assert.Equal("Alpha", table[1].TeamName);
            Assert.Equal(3, table[1].Points);
            Assert.Equal("Beta", table[2].TeamName);
            Assert.Equal(1, table[2].Points);
            Assert.Equal(1, table[0].Position);
        }

        [Fact]
        public void UnplayedFixturesAreIgnored_ButTeamsStillListed()
        {
            var fixtures = new List<Fixture>
            {
                new() { HomeTeamId = 1, AwayTeamId = 2, Played = false },
            };

            var table = StandingsCalculator.Calculate(fixtures, Names);
            Assert.Equal(2, table.Count);
            Assert.All(table, r => Assert.Equal(0, r.Played));
        }

        [Fact]
        public void Form_TracksLastFiveResultsInChronologicalOrder()
        {
            var fixtures = new List<Fixture>();
            // Alpha (id 1): W, W, D, L, W, L across matchdays 1-6 (only last 5 should show).
            var results = new[] { (1, 0), (2, 0), (1, 1), (0, 1), (3, 0), (0, 2) };
            for (int md = 1; md <= 6; md++)
            {
                var (homeGoals, awayGoals) = results[md - 1];
                fixtures.Add(new Fixture
                {
                    Matchday = md, HomeTeamId = 1, AwayTeamId = 2,
                    Played = true, HomeGoals = homeGoals, AwayGoals = awayGoals,
                });
            }

            var table = StandingsCalculator.Calculate(fixtures, Names);
            var alpha = table.Single(r => r.TeamId == 1);

            // Full sequence is W,W,D,L,W,L -> last 5 (dropping the oldest W) = W,D,L,W,L.
            Assert.Equal("WDLWL", alpha.Form);
        }
    }
}
