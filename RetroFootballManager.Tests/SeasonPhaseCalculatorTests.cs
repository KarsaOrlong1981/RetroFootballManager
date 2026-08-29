using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class SeasonPhaseCalculatorTests
    {
        private static List<Fixture> BuildSeasonFixtures() =>
            FixtureGenerator.GenerateLeagueFixtures(
                Enumerable.Range(1, 18).ToList(), season: 1, leagueTier: 1, new DateTime(2026, 8, 1));

        [Fact]
        public void BeforeFirstFixture_IsPreSeason_WindowOpen()
        {
            var fixtures = BuildSeasonFixtures();
            var info = SeasonPhaseCalculator.Calculate(new DateTime(2026, 7, 1), matchdayIndex: 0, fixtures);

            Assert.Equal(SeasonPhase.PreSeason, info.Phase);
            Assert.Equal(TransferWindowState.Open, info.TransferWindow);
        }

        [Fact]
        public void FirstFourMatchdaysOfFirstHalf_WindowOpen()
        {
            var fixtures = BuildSeasonFixtures();
            var md4Date = fixtures.Where(f => f.Matchday == 4).Max(f => f.Date);
            var info = SeasonPhaseCalculator.Calculate(md4Date, matchdayIndex: 4, fixtures);

            Assert.Equal(SeasonPhase.FirstHalf, info.Phase);
            Assert.Equal(TransferWindowState.Open, info.TransferWindow);
        }

        [Fact]
        public void MidFirstHalf_WindowClosed()
        {
            var fixtures = BuildSeasonFixtures();
            var md10Date = fixtures.Where(f => f.Matchday == 10).Max(f => f.Date);
            var info = SeasonPhaseCalculator.Calculate(md10Date, matchdayIndex: 10, fixtures);

            Assert.Equal(SeasonPhase.FirstHalf, info.Phase);
            Assert.Equal(TransferWindowState.Closed, info.TransferWindow);
        }

        [Fact]
        public void DuringWinterBreak_WindowOpen()
        {
            var fixtures = BuildSeasonFixtures();
            int firstHalfCount = fixtures.Max(f => f.Matchday) / 2;
            var lastHinrundeDate = fixtures.Where(f => f.Matchday == firstHalfCount).Max(f => f.Date);
            var breakDate = lastHinrundeDate.AddDays(7);

            var info = SeasonPhaseCalculator.Calculate(breakDate, matchdayIndex: firstHalfCount, fixtures);

            Assert.Equal(SeasonPhase.WinterBreak, info.Phase);
            Assert.Equal(TransferWindowState.Open, info.TransferWindow);
        }

        [Fact]
        public void FirstFourMatchdaysOfSecondHalf_WindowOpen()
        {
            var fixtures = BuildSeasonFixtures();
            int firstHalfCount = fixtures.Max(f => f.Matchday) / 2;
            int matchday = firstHalfCount + 4;
            var date = fixtures.Where(f => f.Matchday == matchday).Max(f => f.Date);

            var info = SeasonPhaseCalculator.Calculate(date, matchdayIndex: matchday, fixtures);

            Assert.Equal(SeasonPhase.SecondHalf, info.Phase);
            Assert.Equal(TransferWindowState.Open, info.TransferWindow);
        }

        [Fact]
        public void MidSecondHalf_WindowClosed()
        {
            var fixtures = BuildSeasonFixtures();
            int firstHalfCount = fixtures.Max(f => f.Matchday) / 2;
            int matchday = firstHalfCount + 10;
            var date = fixtures.Where(f => f.Matchday == matchday).Max(f => f.Date);

            var info = SeasonPhaseCalculator.Calculate(date, matchdayIndex: matchday, fixtures);

            Assert.Equal(SeasonPhase.SecondHalf, info.Phase);
            Assert.Equal(TransferWindowState.Closed, info.TransferWindow);
        }

        // M7: AdvanceToNextSeasonAsync setzt CurrentDate jetzt auf SeasonStart.AddMonths(-2) statt
        // direkt auf SeasonStart - die neue Saison-Fixtures existieren zu diesem Zeitpunkt bereits.
        [Fact]
        public void TwoMonthsBeforeSeasonStart_IsPreSeason_WindowOpen()
        {
            var seasonStart = new DateTime(2026, 8, 1);
            var fixtures = FixtureGenerator.GenerateLeagueFixtures(
                Enumerable.Range(1, 18).ToList(), season: 1, leagueTier: 1, seasonStart);

            var info = SeasonPhaseCalculator.Calculate(seasonStart.AddMonths(-2), matchdayIndex: 0, fixtures);

            Assert.Equal(SeasonPhase.PreSeason, info.Phase);
            Assert.Equal(TransferWindowState.Open, info.TransferWindow);
        }

        [Fact]
        public void IsTransferWindowOpen_MatchesCalculatesTransferWindow()
        {
            var fixtures = BuildSeasonFixtures();
            var md10Date = fixtures.Where(f => f.Matchday == 10).Max(f => f.Date);

            Assert.False(SeasonPhaseCalculator.IsTransferWindowOpen(md10Date, 10, fixtures));
            Assert.True(SeasonPhaseCalculator.IsTransferWindowOpen(new DateTime(2026, 7, 1), 0, fixtures));
        }

        [Fact]
        public void NoFixturesYet_IsPreSeason_WindowOpen()
        {
            var info = SeasonPhaseCalculator.Calculate(new DateTime(2026, 8, 1), matchdayIndex: 0, new List<Fixture>());

            Assert.Equal(SeasonPhase.PreSeason, info.Phase);
            Assert.Equal(TransferWindowState.Open, info.TransferWindow);
        }
    }
}
