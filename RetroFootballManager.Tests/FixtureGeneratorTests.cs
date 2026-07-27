using RetroFootballManager.Common;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class FixtureGeneratorTests
    {
        private static List<int> Teams(int n) => Enumerable.Range(1, n).ToList();

        [Fact]
        public void EighteenTeams_Produce34MatchdaysAnd306Fixtures()
        {
            var fixtures = FixtureGenerator.GenerateLeagueFixtures(
                Teams(18), season: 1, leagueTier: 4, new DateTime(2026, 8, 1));

            Assert.Equal(34, fixtures.Max(f => f.Matchday));
            // 18 Teams, jeder gegen jeden Hin+Rück = 18*17 = 306 Partien.
            Assert.Equal(306, fixtures.Count);
        }

        [Fact]
        public void EachTeam_PlaysEveryOtherTeamHomeAndAwayExactlyOnce()
        {
            var fixtures = FixtureGenerator.GenerateLeagueFixtures(
                Teams(18), season: 1, leagueTier: 1, new DateTime(2026, 8, 1));

            var pairings = fixtures.Select(f => (f.HomeTeamId, f.AwayTeamId)).ToList();
            Assert.Equal(pairings.Count, pairings.Distinct().Count());

            foreach (var a in Teams(18))
            foreach (var b in Teams(18).Where(x => x != a))
            {
                Assert.Contains((a, b), pairings);
            }
        }

        [Fact]
        public void EachTeam_HasBalancedHomeAndAwayCount()
        {
            var fixtures = FixtureGenerator.GenerateLeagueFixtures(
                Teams(18), season: 1, leagueTier: 1, new DateTime(2026, 8, 1));

            foreach (var team in Teams(18))
            {
                int home = fixtures.Count(f => f.HomeTeamId == team);
                int away = fixtures.Count(f => f.AwayTeamId == team);
                Assert.Equal(17, home);
                Assert.Equal(17, away);
            }
        }

        [Fact]
        public void EveryMatchday_HasNineMatches_FiveSaturdayFourSunday()
        {
            var start = new DateTime(2026, 8, 1); // Saturday
            var fixtures = FixtureGenerator.GenerateLeagueFixtures(
                Teams(18), season: 1, leagueTier: 1, start);

            foreach (var group in fixtures.GroupBy(f => f.Matchday))
            {
                Assert.Equal(9, group.Count());
                Assert.Equal(5, group.Count(f => f.Date.DayOfWeek == DayOfWeek.Saturday));
                Assert.Equal(4, group.Count(f => f.Date.DayOfWeek == DayOfWeek.Sunday));
            }
        }

        [Fact]
        public void Matchdays_AreScheduledOneWeekApart()
        {
            var start = FixtureGenerator.FirstSaturdayOnOrAfter(new DateTime(2026, 8, 1));
            var fixtures = FixtureGenerator.GenerateLeagueFixtures(
                Teams(18), season: 1, leagueTier: 1, start);

            var md1 = fixtures.Where(f => f.Matchday == 1).Min(f => f.Date);
            var md2 = fixtures.Where(f => f.Matchday == 2).Min(f => f.Date);
            Assert.Equal(7, (md2 - md1).Days);
        }

        [Fact]
        public void WinterBreak_InsertsGapBetweenFirstAndSecondHalf()
        {
            var start = FixtureGenerator.FirstSaturdayOnOrAfter(new DateTime(2026, 8, 1));
            var fixtures = FixtureGenerator.GenerateLeagueFixtures(
                Teams(18), season: 1, leagueTier: 1, start);

            int firstHalfCount = fixtures.Max(f => f.Matchday) / 2;
            var lastHinrundeDate = fixtures.Where(f => f.Matchday == firstHalfCount).Max(f => f.Date);
            var firstRueckrundeDate = fixtures.Where(f => f.Matchday == firstHalfCount + 1).Min(f => f.Date);

            // lastHinrundeDate is the Sunday of the last Hinrunde matchday, firstRueckrundeDate is
            // the Saturday of the first Rückrunde matchday one day short of a full extra week.
            Assert.Equal(7 * (FixtureGenerator.WinterBreakWeeks + 1) - 1, (firstRueckrundeDate - lastHinrundeDate).Days);
        }

        [Fact]
        public void OddTeamCount_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                FixtureGenerator.GenerateLeagueFixtures(Teams(17), 1, 1, new DateTime(2026, 8, 1)));
        }

        [Fact]
        public void FirstSaturdayOnOrAfter_ReturnsSaturday()
        {
            // 2026-07-23 is a Thursday.
            var sat = FixtureGenerator.FirstSaturdayOnOrAfter(new DateTime(2026, 7, 23));
            Assert.Equal(DayOfWeek.Saturday, sat.DayOfWeek);
            Assert.Equal(new DateTime(2026, 7, 25), sat);
        }
    }
}
