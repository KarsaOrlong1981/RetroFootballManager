using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class ScoutingServiceTests
    {
        private static Employee CreateScout(int ability) => new() { EmployeeType = EmployeeType.Scout, ScoutingAbility = ability };

        [Fact]
        public void HasScout_TrueOnlyWhenScoutEmployed()
        {
            var team = TestHelpers.CreateTeam("Team", baseRating: 60);
            Assert.False(ScoutingService.HasScout(team));

            team.Employees.Add(CreateScout(50));
            Assert.True(ScoutingService.HasScout(team));
        }

        [Fact]
        public void BestScoutingAbility_ReturnsMaxAcrossMultipleScouts()
        {
            var team = TestHelpers.CreateTeam("Team", baseRating: 60);
            team.Employees.Add(CreateScout(40));
            team.Employees.Add(CreateScout(80));
            team.Employees.Add(new Employee { EmployeeType = EmployeeType.Analyst, AnalysisAbility = 99 });

            Assert.Equal(80, ScoutingService.BestScoutingAbility(team));
        }

        [Fact]
        public void BestScoutingAbility_NullWithoutScout()
        {
            var team = TestHelpers.CreateTeam("Team", baseRating: 60);
            Assert.Null(ScoutingService.BestScoutingAbility(team));
        }

        [Fact]
        public void TryStartScouting_RejectsWithoutScout()
        {
            var team = TestHelpers.CreateTeam("Team", baseRating: 60);
            var player = new Player { Id = 99, IsScouted = false };

            bool ok = ScoutingService.TryStartScouting(team, player, out string? error);

            Assert.False(ok);
            Assert.NotNull(error);
        }

        [Fact]
        public void TryStartScouting_RejectsAlreadyScoutedPlayer()
        {
            var team = TestHelpers.CreateTeam("Team", baseRating: 60);
            team.Employees.Add(CreateScout(50));
            var player = new Player { Id = 99, IsScouted = true };

            bool ok = ScoutingService.TryStartScouting(team, player, out string? error);

            Assert.False(ok);
            Assert.NotNull(error);
        }

        [Fact]
        public void TryStartScouting_SucceedsWithScoutAndUnscoutedPlayer()
        {
            var team = TestHelpers.CreateTeam("Team", baseRating: 60);
            team.Employees.Add(CreateScout(50));
            var player = new Player { Id = 99, IsScouted = false };

            bool ok = ScoutingService.TryStartScouting(team, player, out string? error);

            Assert.True(ok);
            Assert.Null(error);
        }

        [Fact]
        public void CreateAssignment_CompletionDateIsFourteenDaysLater()
        {
            var start = new DateTime(2026, 8, 1);
            var assignment = ScoutingService.CreateAssignment(teamId: 1, playerId: 2, start, scoutEmployeeId: 5);

            Assert.Equal(start, assignment.StartDate);
            Assert.Equal(start.AddDays(14), assignment.CompletionDate);
        }

        [Fact]
        public void GetRecommendations_OnlyReturnsCandidatesFromSameLeagueTier()
        {
            var team = TestHelpers.CreateTeam("Own", baseRating: 60);
            team.Id = 1;
            team.LeagueTier = 2;
            team.Employees.Add(CreateScout(70));
            // Own squad only has one Goalkeeper and two Forwards -> everything else is "weak"
            // (< 2 players), so a candidate at any thin position should surface.

            var sameTier = TestHelpers.CreateTeam("SameTier", baseRating: 65);
            sameTier.Id = 2;
            sameTier.LeagueTier = 2;

            var otherTier = TestHelpers.CreateTeam("OtherTier", baseRating: 65);
            otherTier.Id = 3;
            otherTier.LeagueTier = 1;

            var allTeams = new List<Team> { team, sameTier, otherTier };

            var recommendations = ScoutingService.GetRecommendations(team, allTeams, season: 1, month: 1, take: 20);

            Assert.NotEmpty(recommendations);
            Assert.All(recommendations, r => Assert.DoesNotContain(otherTier.Players, p => p.Id == r.PlayerId));
        }

        [Fact]
        public void GetRecommendations_ExcludesAlreadyScoutedPlayers()
        {
            var team = TestHelpers.CreateTeam("Own", baseRating: 60);
            team.Id = 1;
            team.LeagueTier = 2;
            team.Employees.Add(CreateScout(70));

            var sameTier = TestHelpers.CreateTeam("SameTier", baseRating: 65);
            sameTier.Id = 2;
            sameTier.LeagueTier = 2;
            foreach (var p in sameTier.Players)
                p.IsScouted = true;

            var recommendations = ScoutingService.GetRecommendations(team, [team, sameTier], season: 1, month: 1, take: 20);

            Assert.Empty(recommendations);
        }

        [Fact]
        public void GetRecommendations_EmptyWithoutScout()
        {
            var team = TestHelpers.CreateTeam("Own", baseRating: 60);
            team.Id = 1;
            team.LeagueTier = 2;
            var sameTier = TestHelpers.CreateTeam("SameTier", baseRating: 65);
            sameTier.Id = 2;
            sameTier.LeagueTier = 2;

            var recommendations = ScoutingService.GetRecommendations(team, [team, sameTier], season: 1, month: 1);

            Assert.Empty(recommendations);
        }

        [Fact]
        public void GetRecommendations_DeterministicWithinSameMonth_ButCanDifferAcrossMonths()
        {
            var team = TestHelpers.CreateTeam("Own", baseRating: 60);
            team.Id = 1;
            team.LeagueTier = 2;
            team.Employees.Add(CreateScout(50));

            var sameTier = TestHelpers.CreateTeam("SameTier", baseRating: 65);
            sameTier.Id = 2;
            sameTier.LeagueTier = 2;
            var allTeams = new List<Team> { team, sameTier };

            var first = ScoutingService.GetRecommendations(team, allTeams, season: 1, month: 3, take: 20);
            var second = ScoutingService.GetRecommendations(team, allTeams, season: 1, month: 3, take: 20);

            Assert.Equal(first.Select(r => r.PlayerId), second.Select(r => r.PlayerId));
        }
    }
}
