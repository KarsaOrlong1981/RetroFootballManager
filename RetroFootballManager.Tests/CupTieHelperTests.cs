using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class CupTieHelperTests
    {
        [Theory]
        [InlineData(CompetitionType.ChampionsLeague, CupDrawService.RoundLastSixteen, true)]
        [InlineData(CompetitionType.ChampionsLeague, CupDrawService.RoundQuarterFinal, true)]
        [InlineData(CompetitionType.ChampionsLeague, CupDrawService.RoundSemiFinal, true)]
        [InlineData(CompetitionType.EuropaCup, CupDrawService.RoundLastSixteen, true)]
        [InlineData(CompetitionType.ChampionsLeague, CupDrawService.RoundFinal, false)]
        [InlineData(CompetitionType.ChampionsLeague, 0, false)]
        [InlineData(CompetitionType.GermanCup, CupDrawService.RoundLastSixteen, false)]
        [InlineData(CompetitionType.GermanCup, CupDrawService.RoundFinal, false)]
        public void IsTwoLegged_MatchesExpectation(CompetitionType competition, int round, bool expected)
        {
            Assert.Equal(expected, CupTieHelper.IsTwoLegged(competition, round));
        }

        private static CupTie Leg(int homeId, int awayId, int homeGoals, int awayGoals, int legNumber,
            bool wentToPenalties = false, int? penHome = null, int? penAway = null) => new()
        {
            CompetitionType = CompetitionType.ChampionsLeague, Season = 1,
            Round = CupDrawService.RoundLastSixteen, MatchNumberInRound = 1,
            HomeTeamId = homeId, AwayTeamId = awayId, HomeGoals = homeGoals, AwayGoals = awayGoals,
            Played = true, LegNumber = legNumber, WentToPenalties = wentToPenalties,
            PenaltyHomeGoals = penHome, PenaltyAwayGoals = penAway,
        };

        [Fact]
        public void DetermineAggregateWinner_SingleLeg_ReturnsMatchWinner()
        {
            var tie = Leg(1, 2, 2, 0, CupTie.LegNone);
            Assert.Equal(1, CupTieHelper.DetermineAggregateWinner([tie]));
        }

        [Fact]
        public void DetermineAggregateWinner_TwoLegs_SumsGoalsAcrossBothLegs()
        {
            var leg1 = Leg(1, 2, 1, 0, CupTie.LegFirst); // Team1 1:0
            var leg2 = Leg(2, 1, 2, 0, CupTie.LegSecond); // Team2 2:0 -> aggregat Team2 2:1
            Assert.Equal(2, CupTieHelper.DetermineAggregateWinner([leg1, leg2]));
        }

        [Fact]
        public void DetermineAggregateWinner_TiedAggregate_UsesLeg2PenaltyResult()
        {
            var leg1 = Leg(1, 2, 1, 1, CupTie.LegFirst);
            var leg2 = Leg(2, 1, 1, 1, CupTie.LegSecond, wentToPenalties: true, penHome: 3, penAway: 4);
            // Aggregat 2:2, Rückspiel-Elfmeter: Heim(=Team2)=3, Auswärts(=Team1)=4 -> Team1 gewinnt
            Assert.Equal(1, CupTieHelper.DetermineAggregateWinner([leg1, leg2]));
        }

        [Fact]
        public void IsAggregateTied_TrueWhenSumsEqual_FalseOtherwise()
        {
            var leg1 = Leg(1, 2, 1, 1, CupTie.LegFirst);
            var tiedLeg2 = Leg(2, 1, 1, 1, CupTie.LegSecond);
            var notTiedLeg2 = Leg(2, 1, 2, 0, CupTie.LegSecond);

            Assert.True(CupTieHelper.IsAggregateTied(leg1, tiedLeg2));
            Assert.False(CupTieHelper.IsAggregateTied(leg1, notTiedLeg2));
        }

        [Fact]
        public void RequiresPenaltyShootout_GroupStage_AlwaysFalse()
        {
            var tie = new CupTie { Round = 0, HomeGoals = 1, AwayGoals = 1, Played = true };
            Assert.False(CupTieHelper.RequiresPenaltyShootout(tie));
        }

        [Fact]
        public void RequiresPenaltyShootout_FirstLeg_AlwaysFalseEvenOnDraw()
        {
            var tie = Leg(1, 2, 1, 1, CupTie.LegFirst);
            Assert.False(CupTieHelper.RequiresPenaltyShootout(tie));
        }

        [Fact]
        public void RequiresPenaltyShootout_SecondLeg_TrueOnlyWhenAggregateTied()
        {
            var leg1 = Leg(1, 2, 1, 1, CupTie.LegFirst);
            var tiedLeg2 = Leg(2, 1, 1, 1, CupTie.LegSecond);
            var notTiedLeg2 = Leg(2, 1, 1, 0, CupTie.LegSecond); // Aggregat Team1=1, Team2=2 - nicht ausgeglichen

            Assert.True(CupTieHelper.RequiresPenaltyShootout(tiedLeg2, leg1));
            Assert.False(CupTieHelper.RequiresPenaltyShootout(notTiedLeg2, leg1));
        }

        [Fact]
        public void RequiresPenaltyShootout_SingleMatchKoOrFinal_TrueOnDraw()
        {
            var koTie = new CupTie { Round = CupDrawService.RoundLastThirtyTwo, HomeGoals = 1, AwayGoals = 1, Played = true };
            var finalTie = new CupTie { Round = CupDrawService.RoundFinal, HomeGoals = 0, AwayGoals = 0, Played = true };

            Assert.True(CupTieHelper.RequiresPenaltyShootout(koTie));
            Assert.True(CupTieHelper.RequiresPenaltyShootout(finalTie));
        }
    }
}
