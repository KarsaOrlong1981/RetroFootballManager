using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class CupParticipationServiceTests
    {
        private static CupTie Tie(int round, int matchNumber, int homeId, int awayId, bool played,
            int homeGoals = 0, int awayGoals = 0, int legNumber = CupTie.LegNone, string? group = null) => new()
        {
            CompetitionType = CompetitionType.GermanCup, Season = 1, Round = round,
            MatchNumberInRound = matchNumber, Group = group, HomeTeamId = homeId, AwayTeamId = awayId,
            Played = played, HomeGoals = homeGoals, AwayGoals = awayGoals, LegNumber = legNumber,
        };

        [Fact]
        public void GetStatus_TeamNeverAppearsInTies_ReturnsNotEntered()
        {
            var ties = new[] { Tie(1, 1, 10, 20, played: true, homeGoals: 1, awayGoals: 0) };
            Assert.Equal(CupParticipationStatus.NotEntered, CupParticipationService.GetStatus(99, ties));
        }

        [Fact]
        public void GetStatus_LastTieNotPlayedYet_ReturnsStillIn()
        {
            var ties = new[] { Tie(1, 1, 10, 20, played: false) };
            Assert.Equal(CupParticipationStatus.StillIn, CupParticipationService.GetStatus(10, ties));
        }

        [Fact]
        public void GetStatus_LostLastRound_ReturnsEliminated()
        {
            var ties = new[] { Tie(CupDrawService.RoundQuarterFinal, 1, 10, 20, played: true, homeGoals: 0, awayGoals: 2) };
            Assert.Equal(CupParticipationStatus.Eliminated, CupParticipationService.GetStatus(10, ties));
        }

        [Fact]
        public void GetStatus_WonIntermediateRound_ReturnsStillIn()
        {
            var ties = new[] { Tie(CupDrawService.RoundQuarterFinal, 1, 10, 20, played: true, homeGoals: 2, awayGoals: 0) };
            Assert.Equal(CupParticipationStatus.StillIn, CupParticipationService.GetStatus(10, ties));
        }

        [Fact]
        public void GetStatus_WonFinal_ReturnsWon()
        {
            var ties = new[] { Tie(CupDrawService.RoundFinal, 1, 10, 20, played: true, homeGoals: 2, awayGoals: 0) };
            Assert.Equal(CupParticipationStatus.Won, CupParticipationService.GetStatus(10, ties));
        }

        [Fact]
        public void GetStatus_GroupStageStillPlaying_ReturnsStillIn()
        {
            var ties = new[]
            {
                Tie(0, 1, 10, 20, played: true, homeGoals: 1, awayGoals: 0, group: "A"),
                Tie(0, 2, 10, 30, played: false, group: "A"),
            };
            Assert.Equal(CupParticipationStatus.StillIn, CupParticipationService.GetStatus(10, ties));
        }

        [Fact]
        public void GetStatus_GroupStageFinished_Top2Advance_RestEliminated()
        {
            // 4-team group, single round: team 10 wins both -> 1st, team 20 splits -> 2nd,
            // team 30 and 40 lose -> eliminated.
            var ties = new[]
            {
                Tie(0, 1, 10, 20, played: true, homeGoals: 2, awayGoals: 0, group: "A"),
                Tie(0, 2, 30, 40, played: true, homeGoals: 0, awayGoals: 1, group: "A"),
                Tie(0, 3, 10, 30, played: true, homeGoals: 1, awayGoals: 0, group: "A"),
                Tie(0, 4, 20, 40, played: true, homeGoals: 2, awayGoals: 0, group: "A"),
            };

            Assert.Equal(CupParticipationStatus.StillIn, CupParticipationService.GetStatus(10, ties));
            Assert.Equal(CupParticipationStatus.StillIn, CupParticipationService.GetStatus(20, ties));
            Assert.Equal(CupParticipationStatus.Eliminated, CupParticipationService.GetStatus(30, ties));
            Assert.Equal(CupParticipationStatus.Eliminated, CupParticipationService.GetStatus(40, ties));
        }

        [Fact]
        public void GetStatus_TwoLeggedTie_UsesAggregateAcrossBothLegs()
        {
            var ties = new[]
            {
                Tie(CupDrawService.RoundLastSixteen, 1, 10, 20, played: true, homeGoals: 1, awayGoals: 0, legNumber: CupTie.LegFirst),
                Tie(CupDrawService.RoundLastSixteen, 1, 20, 10, played: true, homeGoals: 2, awayGoals: 0, legNumber: CupTie.LegSecond),
            };
            // Aggregate: team 20 wins 2:1 -> team 10 eliminated.
            Assert.Equal(CupParticipationStatus.Eliminated, CupParticipationService.GetStatus(10, ties));
            Assert.Equal(CupParticipationStatus.StillIn, CupParticipationService.GetStatus(20, ties));
        }
    }
}
