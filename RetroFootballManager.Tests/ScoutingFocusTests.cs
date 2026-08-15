using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class ScoutingFocusTests
    {
        // --- FindScoutWithCapacity / TryAssignFocus ---

        [Fact]
        public void FindScoutWithCapacity_ReturnsNull_WhenTeamHasNoScout()
        {
            var team = TestHelpers.CreateTeam("Ohne Scout", baseRating: 60);
            var result = ScoutingService.FindScoutWithCapacity(team, []);
            Assert.Null(result);
        }

        [Fact]
        public void FindScoutWithCapacity_ReturnsNull_WhenAllScoutsAtCapacity()
        {
            var team = TestHelpers.CreateTeam("Voll", baseRating: 60);
            var scout = new Employee { Id = 1, EmployeeType = EmployeeType.Scout, ScoutingAbility = 60 };
            team.Employees.Add(scout);

            var assignments = Enumerable.Range(1, ScoutingService.MaxConcurrentAssignmentsPerScout)
                .Select(i => new ScoutingAssignment { TeamId = team.Id, PlayerId = i, ScoutEmployeeId = scout.Id })
                .ToList();

            var result = ScoutingService.FindScoutWithCapacity(team, assignments);
            Assert.Null(result);
        }

        [Fact]
        public void FindScoutWithCapacity_PicksLeastLoadedScout()
        {
            var team = TestHelpers.CreateTeam("Zwei Scouts", baseRating: 60);
            var busyScout = new Employee { Id = 1, EmployeeType = EmployeeType.Scout, ScoutingAbility = 60 };
            var freeScout = new Employee { Id = 2, EmployeeType = EmployeeType.Scout, ScoutingAbility = 60 };
            team.Employees.Add(busyScout);
            team.Employees.Add(freeScout);

            var assignments = Enumerable.Range(1, 5)
                .Select(i => new ScoutingAssignment { TeamId = team.Id, PlayerId = i, ScoutEmployeeId = busyScout.Id })
                .ToList();

            var result = ScoutingService.FindScoutWithCapacity(team, assignments);
            Assert.Equal(freeScout.Id, result!.Id);
        }

        [Fact]
        public void TryAssignFocus_RejectsWhenScoutAtCapacity()
        {
            var scout = new Employee { Id = 1, Name = "Scoutini", EmployeeType = EmployeeType.Scout, ScoutingAbility = 60 };
            var assignments = Enumerable.Range(1, ScoutingService.MaxConcurrentAssignmentsPerScout)
                .Select(i => new ScoutingAssignment { PlayerId = i, ScoutEmployeeId = scout.Id })
                .ToList();

            bool allowed = ScoutingService.TryAssignFocus(scout, assignments, out string? error);

            Assert.False(allowed);
            Assert.NotNull(error);
        }

        [Fact]
        public void TryAssignFocus_AllowsWhenScoutHasFreeCapacity()
        {
            var scout = new Employee { Id = 1, Name = "Scoutini", EmployeeType = EmployeeType.Scout, ScoutingAbility = 60 };
            var assignments = new List<ScoutingAssignment>
            {
                new() { PlayerId = 1, ScoutEmployeeId = scout.Id },
            };

            bool allowed = ScoutingService.TryAssignFocus(scout, assignments, out string? error);

            Assert.True(allowed);
            Assert.Null(error);
        }

        // --- EvaluatePositionAgainstLeague ---

        [Fact]
        public void EvaluatePositionAgainstLeague_TooFewPlayers_IsAlwaysWeak()
        {
            var team = TestHelpers.CreateTeam("Wenig Stürmer", baseRating: 70);
            // TestHelpers fields exactly 2 Forwards - below the new MinPlayersPerPosition (3).
            var league = new List<Team> { team };

            bool weak = ScoutingService.EvaluatePositionAgainstLeague(team, Position.Forward, league, scoutAbility: 90, new Random(1));

            Assert.True(weak);
        }

        [Fact]
        public void EvaluatePositionAgainstLeague_HighScoutAbility_StaysCloseToTrueLeagueAverage()
        {
            var strongTeam = TestHelpers.CreateTeam("Stark", baseRating: 90);
            strongTeam.Id = 1;
            foreach (var p in strongTeam.Players.Where(p => p.Position == Position.CentralDefender))
                p.Rating = 95;
            // TestHelpers' fixed formation only has 2 CentralDefenders - add a 3rd so the
            // "too few players" branch (MinPlayersPerPosition=3) doesn't short-circuit the test.
            strongTeam.Players.Add(new Player { Id = 9001, Position = Position.CentralDefender, Rating = 95 });
            var weakOpponent = TestHelpers.CreateTeam("Schwach", baseRating: 20);
            weakOpponent.Id = 2;
            var league = new List<Team> { strongTeam, weakOpponent };

            // Strong team's central defenders (95) are clearly far above a weak league average -
            // a highly able scout should almost always correctly assess this position as fine.
            int weakCount = 0;
            for (int seed = 0; seed < 30; seed++)
            {
                bool weak = ScoutingService.EvaluatePositionAgainstLeague(
                    strongTeam, Position.CentralDefender, league, scoutAbility: 99, new Random(seed));
                if (weak) weakCount++;
            }

            Assert.True(weakCount < 5, $"weakCount={weakCount}");
        }

        [Fact]
        public void EvaluatePositionAgainstLeague_LowScoutAbility_CanMisjudgeMoreOften()
        {
            var strongTeam = TestHelpers.CreateTeam("Stark", baseRating: 90);
            strongTeam.Id = 1;
            foreach (var p in strongTeam.Players.Where(p => p.Position == Position.CentralDefender))
                p.Rating = 95;
            strongTeam.Players.Add(new Player { Id = 9001, Position = Position.CentralDefender, Rating = 95 });
            var weakOpponent = TestHelpers.CreateTeam("Schwach", baseRating: 20);
            weakOpponent.Id = 2;
            var league = new List<Team> { strongTeam, weakOpponent };

            int weakCountHighAbility = 0;
            int weakCountLowAbility = 0;
            for (int seed = 0; seed < 30; seed++)
            {
                if (ScoutingService.EvaluatePositionAgainstLeague(strongTeam, Position.CentralDefender, league, 99, new Random(seed)))
                    weakCountHighAbility++;
                if (ScoutingService.EvaluatePositionAgainstLeague(strongTeam, Position.CentralDefender, league, 5, new Random(seed)))
                    weakCountLowAbility++;
            }

            Assert.True(weakCountLowAbility >= weakCountHighAbility,
                $"low={weakCountLowAbility}, high={weakCountHighAbility}");
        }

        // --- FindCandidatesForFocus ---

        [Fact]
        public void FindCandidatesForFocus_NoFilters_FallsBackToTeamWeaknessAnalysis()
        {
            var team = TestHelpers.CreateTeam("Schwacher Sturm", baseRating: 60);
            team.Id = 1;
            var otherTeam = TestHelpers.CreateTeam("Anderes Team", baseRating: 60);
            otherTeam.Id = 2;
            foreach (var p in otherTeam.Players.Where(p => p.Position == Position.Forward))
                p.Rating = 95;
            var allTeams = new List<Team> { team, otherTeam };

            foreach (var p in otherTeam.Players) p.TeamId = otherTeam.Id;

            var focus = new ScoutingFocus { ScoutEmployeeId = 1 };
            team.Employees.Add(new Employee { Id = 1, EmployeeType = EmployeeType.Scout, ScoutingAbility = 80 });

            var candidates = ScoutingService.FindCandidatesForFocus(team, focus, allTeams, new Random(1));

            // Forward is understaffed for `team` (only 2, below MinPlayersPerPosition=3), so the
            // fallback should surface the other team's (strong) forwards.
            Assert.NotEmpty(candidates);
            Assert.All(candidates, p => Assert.Equal(otherTeam.Id, p.TeamId));
        }

        [Fact]
        public void FindCandidatesForFocus_PositionFilter_OnlyReturnsThatPosition()
        {
            var team = TestHelpers.CreateTeam("Suchend", baseRating: 60);
            team.Id = 1;
            var otherTeam = TestHelpers.CreateTeam("Angebot", baseRating: 60);
            otherTeam.Id = 2;
            var allTeams = new List<Team> { team, otherTeam };

            var focus = new ScoutingFocus { Position = Position.Forward };

            var candidates = ScoutingService.FindCandidatesForFocus(team, focus, allTeams, new Random(1));

            Assert.NotEmpty(candidates);
            Assert.All(candidates, p => Assert.Equal(Position.Forward, p.Position));
        }

        [Fact]
        public void FindCandidatesForFocus_AttributeFilter_OnlyReturnsPlayersAboveMinimum()
        {
            var team = TestHelpers.CreateTeam("Suchend", baseRating: 60);
            team.Id = 1;
            var otherTeam = TestHelpers.CreateTeam("Angebot", baseRating: 60);
            otherTeam.Id = 2;
            otherTeam.Players[0].PassingAccuracy = 95;
            var allTeams = new List<Team> { team, otherTeam };

            var focus = new ScoutingFocus
            {
                AttributeFilters = [new AttributeFilter(PlayerAttributeType.PassingAccuracy, 90)],
            };

            var candidates = ScoutingService.FindCandidatesForFocus(team, focus, allTeams, new Random(1));

            Assert.All(candidates, p => Assert.True(p.PassingAccuracy >= 90));
        }

        [Fact]
        public void FindCandidatesForFocus_MinAgeMaxAgeFilter_ExcludesOutOfRange()
        {
            var team = TestHelpers.CreateTeam("Suchend", baseRating: 60);
            team.Id = 1;
            var otherTeam = TestHelpers.CreateTeam("Angebot", baseRating: 60);
            otherTeam.Id = 2;
            otherTeam.Players[0].Age = 35;
            otherTeam.Players[1].Age = 22;
            var allTeams = new List<Team> { team, otherTeam };

            var focus = new ScoutingFocus { MinAge = 20, MaxAge = 25 };

            var candidates = ScoutingService.FindCandidatesForFocus(team, focus, allTeams, new Random(1));

            Assert.All(candidates, p => Assert.InRange(p.Age, 20, 25));
        }

        [Fact]
        public void FindCandidatesForFocus_ExcludesAlreadyScoutedAndOwnPlayers()
        {
            var team = TestHelpers.CreateTeam("Suchend", baseRating: 60);
            team.Id = 1;
            var otherTeam = TestHelpers.CreateTeam("Angebot", baseRating: 60);
            otherTeam.Id = 2;
            otherTeam.Players[0].IsScouted = true;
            var allTeams = new List<Team> { team, otherTeam };

            var focus = new ScoutingFocus();

            var candidates = ScoutingService.FindCandidatesForFocus(team, focus, allTeams, new Random(1));

            Assert.DoesNotContain(candidates, p => p.Id == otherTeam.Players[0].Id);
            Assert.DoesNotContain(candidates, p => team.Players.Any(tp => tp.Id == p.Id));
        }
    }
}
