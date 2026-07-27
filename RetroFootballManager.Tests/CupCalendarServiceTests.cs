using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class CupCalendarServiceTests
    {
        private static readonly DateTime SeasonStart = new(2026, 8, 1);

        [Theory]
        [InlineData(CupDrawService.RoundPreliminary)]
        [InlineData(CupDrawService.RoundLastSixtyFour)]
        [InlineData(CupDrawService.RoundLastThirtyTwo)]
        [InlineData(CupDrawService.RoundLastSixteen)]
        [InlineData(CupDrawService.RoundQuarterFinal)]
        [InlineData(CupDrawService.RoundSemiFinal)]
        [InlineData(CupDrawService.RoundFinal)]
        public void GermanCup_RoundDates_AreAlwaysTuesdays(int round)
        {
            var date = CupCalendarService.GetKoRoundDate(CompetitionType.GermanCup, round, SeasonStart);
            Assert.Equal(DayOfWeek.Tuesday, date.DayOfWeek);
        }

        [Theory]
        [InlineData(CupDrawService.RoundLastSixteen)]
        [InlineData(CupDrawService.RoundQuarterFinal)]
        [InlineData(CupDrawService.RoundSemiFinal)]
        [InlineData(CupDrawService.RoundFinal)]
        public void ChampionsLeague_KoDates_AreAlwaysWednesdays(int round)
        {
            var date = CupCalendarService.GetKoRoundDate(CompetitionType.ChampionsLeague, round, SeasonStart);
            Assert.Equal(DayOfWeek.Wednesday, date.DayOfWeek);
        }

        [Theory]
        [InlineData(CupDrawService.RoundLastSixteen)]
        [InlineData(CupDrawService.RoundQuarterFinal)]
        [InlineData(CupDrawService.RoundSemiFinal)]
        [InlineData(CupDrawService.RoundFinal)]
        public void EuropaCup_KoDates_AreAlwaysThursdays(int round)
        {
            var date = CupCalendarService.GetKoRoundDate(CompetitionType.EuropaCup, round, SeasonStart);
            Assert.Equal(DayOfWeek.Thursday, date.DayOfWeek);
        }

        [Fact]
        public void GroupMatchdays_AreOrderedAndSpacedAcrossSeason()
        {
            var dates = Enumerable.Range(0, CupCalendarService.GroupMatchdayCount)
                .Select(i => CupCalendarService.GetGroupMatchdayDate(CompetitionType.ChampionsLeague, i, SeasonStart))
                .ToList();

            for (int i = 1; i < dates.Count; i++)
                Assert.True(dates[i] > dates[i - 1]);
        }

        [Fact]
        public void EuropaCupFinal_IsBeforeChampionsLeagueFinal()
        {
            var elFinal = CupCalendarService.GetKoRoundDate(CompetitionType.EuropaCup, CupDrawService.RoundFinal, SeasonStart);
            var clFinal = CupCalendarService.GetKoRoundDate(CompetitionType.ChampionsLeague, CupDrawService.RoundFinal, SeasonStart);
            Assert.True(elFinal < clFinal);
        }

        [Fact]
        public void GermanCupRounds_AreChronologicallyOrdered()
        {
            int[] rounds =
            [
                CupDrawService.RoundPreliminary, CupDrawService.RoundLastSixtyFour, CupDrawService.RoundLastThirtyTwo,
                CupDrawService.RoundLastSixteen, CupDrawService.RoundQuarterFinal, CupDrawService.RoundSemiFinal,
                CupDrawService.RoundFinal,
            ];

            var dates = rounds.Select(r => CupCalendarService.GetKoRoundDate(CompetitionType.GermanCup, r, SeasonStart)).ToList();
            for (int i = 1; i < dates.Count; i++)
                Assert.True(dates[i] > dates[i - 1]);
        }

        [Theory]
        [InlineData(CompetitionType.ChampionsLeague, CupDrawService.RoundLastSixteen)]
        [InlineData(CompetitionType.ChampionsLeague, CupDrawService.RoundQuarterFinal)]
        [InlineData(CompetitionType.ChampionsLeague, CupDrawService.RoundSemiFinal)]
        [InlineData(CompetitionType.EuropaCup, CupDrawService.RoundLastSixteen)]
        [InlineData(CompetitionType.EuropaCup, CupDrawService.RoundQuarterFinal)]
        [InlineData(CompetitionType.EuropaCup, CupDrawService.RoundSemiFinal)]
        public void GetKoRoundDate_SecondLeg_IsOneWeekAfterFirstLeg(CompetitionType competition, int round)
        {
            var firstLeg = CupCalendarService.GetKoRoundDate(competition, round, SeasonStart);
            var secondLeg = CupCalendarService.GetKoRoundDate(competition, round, SeasonStart, secondLeg: true);

            Assert.Equal(firstLeg.AddDays(7), secondLeg);
        }

        [Fact]
        public void GermanCupFinal_FallsAfterLeagueSeasonEnds()
        {
            // Liga: 17 Hinrunde + 4 Winterpause + 17 Rückrunde Wochen ab dem ersten Spieltag.
            var lastLeagueMatchday = FixtureGenerator.FirstSaturdayOnOrAfter(SeasonStart)
                .AddDays((17 + FixtureGenerator.WinterBreakWeeks + 16) * 7);

            var final = CupCalendarService.GetKoRoundDate(CompetitionType.GermanCup, CupDrawService.RoundFinal, SeasonStart);

            Assert.True(final > lastLeagueMatchday);
        }
    }
}
