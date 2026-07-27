using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class InjuryDurationTests
    {
        private static readonly DateTime MatchDate = new(2026, 9, 1);

        [Fact]
        public void ApplyInjuryDurations_SetsInjuredUntilFromRolledDays()
        {
            var player = TestHelpers.CreateTeam("Team", baseRating: 60).Players[0];
            var result = new MatchResult();
            result.InjuredPlayers.Add(player);
            result.InjuryDurationDays[player.Id] = 10;

            result.ApplyInjuryDurations(MatchDate);

            Assert.Equal(MatchDate.AddDays(10), player.InjuredUntil);
        }

        [Fact]
        public void RecoverForMatch_KeepsPlayerInjured_WhileInjuredUntilInFuture()
        {
            var team = TestHelpers.CreateTeam("Team", baseRating: 60);
            var player = team.Players[0];
            player.Status = PlayerStatus.Injured;
            player.InjuredUntil = MatchDate.AddDays(5);

            MatchDayService.RecoverForMatch(team, MatchDate);

            Assert.Equal(PlayerStatus.Injured, player.Status);
            Assert.NotNull(player.InjuredUntil);
        }

        [Fact]
        public void RecoverForMatch_ClearsInjury_OnceInjuredUntilReached()
        {
            var team = TestHelpers.CreateTeam("Team", baseRating: 60);
            var player = team.Players[0];
            player.Status = PlayerStatus.Injured;
            player.InjuredUntil = MatchDate;

            MatchDayService.RecoverForMatch(team, MatchDate);

            Assert.Equal(PlayerStatus.Available, player.Status);
            Assert.Null(player.InjuredUntil);
        }

        [Fact]
        public void RecoverForMatch_WithoutCurrentDate_AlwaysHealsImmediately()
        {
            var team = TestHelpers.CreateTeam("Team", baseRating: 60);
            var player = team.Players[0];
            player.Status = PlayerStatus.Injured;
            player.InjuredUntil = MatchDate.AddDays(30);

            MatchDayService.RecoverForMatch(team);

            Assert.Equal(PlayerStatus.Available, player.Status);
        }
    }
}
