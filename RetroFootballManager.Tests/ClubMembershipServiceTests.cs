using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class ClubMembershipServiceTests
    {
        private static Team Team(int id, int tier = 1)
        {
            var team = TestHelpers.CreateTeam($"Team {id}", baseRating: 60);
            team.Id = id;
            team.LeagueTier = tier;
            team.Finances = new Finances { ClubMembers = 100_000 };
            return team;
        }

        private static StandingRow Row(int teamId, int played, int points) =>
            new(1, teamId, $"Team {teamId}", Played: played, Wins: 0, Draws: 0, Losses: 0,
                GoalsFor: 0, GoalsAgainst: 0, GoalDifference: 0, Points: points, Form: "");

        private static CupTie Tie(int round, int homeId, int awayId, bool played, int homeGoals = 0, int awayGoals = 0) => new()
        {
            CompetitionType = CompetitionType.GermanCup, Season = 1, Round = round, MatchNumberInRound = 1,
            HomeTeamId = homeId, AwayTeamId = awayId, Played = played, HomeGoals = homeGoals, AwayGoals = awayGoals,
        };

        [Fact]
        public void ForTierAndRating_LowRating_ReturnsTierMinimum()
        {
            var (members, fee) = ClubMembershipService.ForTierAndRating(1, rating: 0);

            Assert.Equal(120_000, members);
            Assert.Equal(80, fee);
        }

        [Fact]
        public void ForTierAndRating_HighRating_ReturnsTierMaximum()
        {
            var (members, fee) = ClubMembershipService.ForTierAndRating(1, rating: 100);

            Assert.Equal(150_000, members);
            Assert.Equal(150, fee);
        }

        [Theory]
        [InlineData(9, true)]
        [InlineData(17, true)]
        [InlineData(26, true)]
        [InlineData(34, true)]
        [InlineData(10, false)]
        public void IsQuarterCheckpoint_OnlyAtQuarterMatchdays(int matchday, bool expected) =>
            Assert.Equal(expected, ClubMembershipService.IsQuarterCheckpoint(matchday));

        [Fact]
        public void ApplyQuarterlyPerformance_StrongQuarter_IncreasesMembers()
        {
            var team = Team(1);
            int delta = ClubMembershipService.ApplyQuarterlyPerformance(team, Row(1, played: 9, points: 27), matchday: 9);

            Assert.Equal(500, delta);
            Assert.Equal(100_500, team.Finances!.ClubMembers);
        }

        [Fact]
        public void ApplyQuarterlyPerformance_PoorQuarter_DecreasesMembers()
        {
            var team = Team(1);
            int delta = ClubMembershipService.ApplyQuarterlyPerformance(team, Row(1, played: 9, points: 3), matchday: 9);

            Assert.Equal(-500, delta);
            Assert.Equal(99_500, team.Finances!.ClubMembers);
        }

        [Fact]
        public void ApplyQuarterlyPerformance_NotAtCheckpoint_NoOp()
        {
            var team = Team(1);
            int delta = ClubMembershipService.ApplyQuarterlyPerformance(team, Row(1, played: 5, points: 15), matchday: 5);

            Assert.Equal(0, delta);
            Assert.Equal(100_000, team.Finances!.ClubMembers);
        }

        [Fact]
        public void ApplyQuarterlyPerformance_SecondQuarter_DiffsAgainstFirstCheckpoint()
        {
            var team = Team(1);
            ClubMembershipService.ApplyQuarterlyPerformance(team, Row(1, played: 9, points: 27), matchday: 9);
            int delta = ClubMembershipService.ApplyQuarterlyPerformance(team, Row(1, played: 17, points: 27), matchday: 17);

            // 0 points across the second quarter's 8 games - poor.
            Assert.Equal(-500, delta);
        }

        [Fact]
        public void ApplySeasonEndAdjustments_Champion_GetsTierChampionshipBonus()
        {
            var team = Team(1, tier: 4);
            var result = new SeasonEndResult(
                Season: 1,
                Leagues: [new LeagueTableResult(Tier: 4, Table: [Row(1, played: 34, points: 90)], PromotedTeamIds: [], RelegatedTeamIds: [])],
                ManagerFinalPosition: 1, ManagerTier: 4, PointsAwarded: 0, ManagerOutcome: "", ManagerPromoted: false, ManagerRelegated: false);

            ClubMembershipService.ApplySeasonEndAdjustments([team], result);

            Assert.Equal(100_250, team.Finances!.ClubMembers);
        }

        [Fact]
        public void ApplySeasonEndAdjustments_Relegated_LosesMembers_AndFeeRescalesToNewTier()
        {
            var team = Team(1, tier: 1);
            team.LeagueTier = 2; // SeasonProgressionService.EndSeason already applied the drop
            var result = new SeasonEndResult(
                Season: 1,
                Leagues: [new LeagueTableResult(Tier: 1, Table: [Row(1, played: 34, points: 20)], PromotedTeamIds: [], RelegatedTeamIds: [1])],
                ManagerFinalPosition: 1, ManagerTier: 1, PointsAwarded: 0, ManagerOutcome: "", ManagerPromoted: false, ManagerRelegated: true);

            ClubMembershipService.ApplySeasonEndAdjustments([team], result);

            // -2500 malus, then reclamped into tier 2's (40k-60k) range since 97,500 is above it.
            Assert.Equal(60_000, team.Finances!.ClubMembers);
            Assert.InRange(team.Finances.MembershipFeePerMember, 60, 130);
        }

        [Fact]
        public void ApplyCupPrizes_GermanCupWinner_GetsCumulativeRoundBonusPlusWinnerBonus()
        {
            var winner = Team(1);
            var runnerUp = Team(2);
            var ties = new[]
            {
                Tie(CupDrawService.RoundLastSixtyFour, winner.Id, 99, played: true, homeGoals: 1, awayGoals: 0),
                Tie(CupDrawService.RoundFinal, winner.Id, runnerUp.Id, played: true, homeGoals: 2, awayGoals: 0),
            };

            ClubMembershipService.ApplyCupPrizes([winner, runnerUp], ties);

            // 30 (round of 64) + 1000 (final reached) + 2000 (winner)
            Assert.Equal(103_030, winner.Finances!.ClubMembers);
        }
    }
}
