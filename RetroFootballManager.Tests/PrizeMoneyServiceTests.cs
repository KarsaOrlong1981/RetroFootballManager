using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class PrizeMoneyServiceTests
    {
        private static Team Team(int id)
        {
            var team = TestHelpers.CreateTeam($"Team {id}", baseRating: 60);
            team.Id = id;
            team.Finances = new Finances { CurrentBalance = 0 };
            return team;
        }

        private static StandingRow Row(int position, int teamId) =>
            new(position, teamId, $"Team {teamId}", Played: 30, Wins: 0, Draws: 0, Losses: 0,
                GoalsFor: 0, GoalsAgainst: 0, GoalDifference: 0, Points: 0, Form: "");

        private static CupTie Tie(int round, int matchNumber, int homeId, int awayId, bool played,
            int homeGoals = 0, int awayGoals = 0, int legNumber = CupTie.LegNone, string? group = null,
            CompetitionType competition = CompetitionType.GermanCup) => new()
        {
            CompetitionType = competition, Season = 1, Round = round, MatchNumberInRound = matchNumber,
            Group = group, HomeTeamId = homeId, AwayTeamId = awayId, Played = played,
            HomeGoals = homeGoals, AwayGoals = awayGoals, LegNumber = legNumber,
        };

        [Fact]
        public void AwardLeaguePrizes_PaysTop3_InLowerLeagues()
        {
            var teams = new[] { Team(1), Team(2), Team(3), Team(4) };
            var result = new SeasonEndResult(
                Season: 1,
                Leagues: [new LeagueTableResult(Tier: 4, Table: [Row(1, 1), Row(2, 2), Row(3, 3), Row(4, 4)], PromotedTeamIds: [], RelegatedTeamIds: [])],
                ManagerFinalPosition: 1, ManagerTier: 4, PointsAwarded: 0, ManagerOutcome: "", ManagerPromoted: false, ManagerRelegated: false);

            PrizeMoneyService.AwardLeaguePrizes(teams, result);

            Assert.Equal(250_000, teams[0].Finances!.CurrentBalance);
            Assert.Equal(190_000, teams[1].Finances!.CurrentBalance);
            Assert.Equal(150_000, teams[2].Finances!.CurrentBalance);
            Assert.Equal(0, teams[3].Finances!.CurrentBalance);
        }

        [Fact]
        public void AwardLeaguePrizes_League1_PaysContinentalQualification()
        {
            var teams = Enumerable.Range(1, 8).Select(Team).ToArray();
            var result = new SeasonEndResult(
                Season: 1,
                Leagues: [new LeagueTableResult(Tier: 1, Table: teams.Select((t, i) => Row(i + 1, t.Id)).ToList(), PromotedTeamIds: [], RelegatedTeamIds: [])],
                ManagerFinalPosition: 1, ManagerTier: 1, PointsAwarded: 0, ManagerOutcome: "", ManagerPromoted: false, ManagerRelegated: false);

            PrizeMoneyService.AwardLeaguePrizes(teams, result);

            for (int i = 0; i < 4; i++)
                Assert.Equal(PrizeMoneyService.ChampionsLeagueQualificationPrize, teams[i].Finances!.CurrentBalance);
            for (int i = 4; i < 7; i++)
                Assert.Equal(PrizeMoneyService.EuropaCupQualificationPrize, teams[i].Finances!.CurrentBalance);
            Assert.Equal(0, teams[7].Finances!.CurrentBalance);
        }

        [Fact]
        public void AwardCupPrizes_GermanCupWinner_GetsCumulativeRoundFeesPlusWinnerBonus()
        {
            var winner = Team(1);
            var runnerUp = Team(2);
            var ties = new[]
            {
                Tie(CupDrawService.RoundLastSixtyFour, 1, winner.Id, 99, played: true, homeGoals: 1, awayGoals: 0),
                Tie(CupDrawService.RoundLastThirtyTwo, 1, winner.Id, 98, played: true, homeGoals: 1, awayGoals: 0),
                Tie(CupDrawService.RoundLastSixteen, 1, winner.Id, 97, played: true, homeGoals: 1, awayGoals: 0),
                Tie(CupDrawService.RoundQuarterFinal, 1, winner.Id, 96, played: true, homeGoals: 1, awayGoals: 0),
                Tie(CupDrawService.RoundSemiFinal, 1, winner.Id, 95, played: true, homeGoals: 1, awayGoals: 0),
                Tie(CupDrawService.RoundFinal, 1, winner.Id, runnerUp.Id, played: true, homeGoals: 2, awayGoals: 0),
            };

            PrizeMoneyService.AwardCupPrizes([winner, runnerUp], ties, CompetitionType.GermanCup);

            // 10k + 20k + 40k + 80k + 150k + 300k (reached-round fees) + 2,000,000 (Sieger)
            Assert.Equal(2_600_000, winner.Finances!.CurrentBalance);
        }

        [Fact]
        public void AwardCupPrizes_GermanCupFinalist_GetsFinalistBonus_NotWinnerBonus()
        {
            var winner = Team(1);
            var runnerUp = Team(2);
            var ties = new[]
            {
                Tie(CupDrawService.RoundFinal, 1, winner.Id, runnerUp.Id, played: true, homeGoals: 2, awayGoals: 0),
            };

            PrizeMoneyService.AwardCupPrizes([winner, runnerUp], ties, CompetitionType.GermanCup);

            Assert.Equal(300_000 + 1_200_000, runnerUp.Finances!.CurrentBalance);
        }

        [Fact]
        public void AwardCupPrizes_GermanCupSemiFinalLoser_GetsSemiFinalOutBonus()
        {
            var loser = Team(1);
            var winner = Team(2);
            var ties = new[]
            {
                Tie(CupDrawService.RoundSemiFinal, 1, loser.Id, winner.Id, played: true, homeGoals: 0, awayGoals: 1),
            };

            PrizeMoneyService.AwardCupPrizes([loser, winner], ties, CompetitionType.GermanCup);

            Assert.Equal(150_000 + 500_000, loser.Finances!.CurrentBalance);
        }

        [Fact]
        public void AwardCupPrizes_EarlyRoundExit_OnlyGetsReachedFee_NoPlacementBonus()
        {
            var loser = Team(1);
            var winner = Team(2);
            var ties = new[]
            {
                Tie(CupDrawService.RoundLastThirtyTwo, 1, loser.Id, winner.Id, played: true, homeGoals: 0, awayGoals: 1),
            };

            PrizeMoneyService.AwardCupPrizes([loser, winner], ties, CompetitionType.GermanCup);

            Assert.Equal(20_000, loser.Finances!.CurrentBalance);
        }

        [Fact]
        public void AwardCupPrizes_UnplayedFinal_SkipsBothTeams()
        {
            var a = Team(1);
            var b = Team(2);
            var ties = new[]
            {
                Tie(CupDrawService.RoundSemiFinal, 1, a.Id, 99, played: true, homeGoals: 1, awayGoals: 0),
                Tie(CupDrawService.RoundFinal, 1, a.Id, b.Id, played: false),
            };

            PrizeMoneyService.AwardCupPrizes([a, b], ties, CompetitionType.GermanCup);

            Assert.Equal(0, a.Finances!.CurrentBalance);
            Assert.Equal(0, b.Finances!.CurrentBalance);
        }

        [Fact]
        public void AwardCupPrizes_ChampionsLeague_GroupStageEliminated_OnlyGetsGroupPhaseFee()
        {
            var team = Team(1);
            var ties = new[]
            {
                Tie(0, 1, team.Id, 20, played: true, homeGoals: 0, awayGoals: 1, group: "A", competition: CompetitionType.ChampionsLeague),
                Tie(0, 2, team.Id, 30, played: true, homeGoals: 0, awayGoals: 2, group: "A", competition: CompetitionType.ChampionsLeague),
            };

            PrizeMoneyService.AwardCupPrizes([team], ties, CompetitionType.ChampionsLeague);

            Assert.Equal(500_000, team.Finances!.CurrentBalance);
        }

        [Fact]
        public void AwardCupPrizes_ChampionsLeague_TwoLeggedQuarterFinalWin_UsesAggregate()
        {
            var team = Team(1);
            var opponent = Team(2);
            var ties = new[]
            {
                Tie(0, 1, team.Id, 20, played: true, homeGoals: 1, awayGoals: 0, group: "A", competition: CompetitionType.ChampionsLeague),
                Tie(CupDrawService.RoundLastSixteen, 1, team.Id, 40, played: true, homeGoals: 2, awayGoals: 0, competition: CompetitionType.ChampionsLeague),
                Tie(CupDrawService.RoundQuarterFinal, 1, team.Id, opponent.Id, played: true,
                    homeGoals: 1, awayGoals: 1, legNumber: CupTie.LegFirst, competition: CompetitionType.ChampionsLeague),
                Tie(CupDrawService.RoundQuarterFinal, 1, opponent.Id, team.Id, played: true,
                    homeGoals: 0, awayGoals: 2, legNumber: CupTie.LegSecond, competition: CompetitionType.ChampionsLeague),
            };

            PrizeMoneyService.AwardCupPrizes([team, opponent], ties, CompetitionType.ChampionsLeague);

            // Group (500k) + Ro16 (300k) + QF reached (600k), aggregate 3:1 win - no placement
            // bonus yet (only reached the quarterfinal, not semifinal/final).
            Assert.Equal(1_400_000, team.Finances!.CurrentBalance);
        }
    }
}
