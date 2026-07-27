using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class ScoutingReportServiceTests
    {
        private static StandingRow OpponentRow(int teamId) =>
            new(Position: 5, TeamId: teamId, TeamName: "Opponent", Played: 10, Wins: 4, Draws: 2, Losses: 4,
                GoalsFor: 15, GoalsAgainst: 14, GoalDifference: 1, Points: 14, Form: "WWDLL");

        private static Team WeakDefenseOpponent(int id)
        {
            var team = TestHelpers.CreateTeam("Opponent", baseRating: 60);
            team.Id = id;
            foreach (var p in team.Players.Where(p => p.Position != Position.Goalkeeper))
            {
                p.DefensivePower = 15;
                p.DuelHardness = 15;
            }
            return team;
        }

        [Fact]
        public void NoAnalyst_OnlyBasicFieldsSet()
        {
            var own = TestHelpers.CreateTeam("Own", baseRating: 60);
            var opponent = WeakDefenseOpponent(id: 2);
            var standings = new List<StandingRow> { OpponentRow(2) };
            var leagueTeams = new List<Team> { opponent, TestHelpers.CreateTeam("Avg", baseRating: 60) };

            var report = ScoutingReportService.BuildReport(own, opponent, standings, leagueTeams, analysisAbility: null);

            Assert.Equal(5, report.OpponentPosition);
            Assert.Equal("WWDLL", report.OpponentForm);
            Assert.Null(report.OpponentProfile);
            Assert.Null(report.LeagueAverageProfile);
            Assert.Null(report.WeaknessCategory);
            Assert.Null(report.TacticSuggestion);
        }

        [Fact]
        public void LowQualityAnalyst_HasProfile_ButNoWeaknessOrStrength()
        {
            var own = TestHelpers.CreateTeam("Own", baseRating: 60);
            var opponent = WeakDefenseOpponent(id: 2);
            var standings = new List<StandingRow> { OpponentRow(2) };
            var leagueTeams = new List<Team> { opponent, TestHelpers.CreateTeam("Avg", baseRating: 60) };

            var report = ScoutingReportService.BuildReport(own, opponent, standings, leagueTeams, analysisAbility: 30);

            Assert.NotNull(report.OpponentProfile);
            Assert.NotNull(report.LeagueAverageProfile);
            Assert.Null(report.WeaknessCategory);
            Assert.Null(report.TacticSuggestion);
        }

        [Fact]
        public void AnalysisAbility50_DetectsWeakDefense()
        {
            var own = TestHelpers.CreateTeam("Own", baseRating: 60);
            var opponent = WeakDefenseOpponent(id: 2);
            var standings = new List<StandingRow> { OpponentRow(2) };
            var leagueTeams = new List<Team>
            {
                opponent,
                TestHelpers.CreateTeam("Avg1", baseRating: 60),
                TestHelpers.CreateTeam("Avg2", baseRating: 60),
            };

            var report = ScoutingReportService.BuildReport(own, opponent, standings, leagueTeams, analysisAbility: 50);

            Assert.Equal("Abwehr", report.WeaknessCategory);
            Assert.Null(report.TacticSuggestion);
        }

        [Fact]
        public void AnalysisAbility75_SuggestsMoreOffensiveTacticAgainstWeakDefense()
        {
            var own = TestHelpers.CreateTeam("Own", baseRating: 60);
            var opponent = WeakDefenseOpponent(id: 2);
            var standings = new List<StandingRow> { OpponentRow(2) };
            var leagueTeams = new List<Team>
            {
                opponent,
                TestHelpers.CreateTeam("Avg1", baseRating: 60),
                TestHelpers.CreateTeam("Avg2", baseRating: 60),
            };

            var report = ScoutingReportService.BuildReport(own, opponent, standings, leagueTeams, analysisAbility: 80);

            Assert.Equal("Abwehr", report.WeaknessCategory);
            Assert.NotNull(report.TacticSuggestion);
            Assert.Equal("Angriff", report.TacticSuggestion!.ExploitedCategory);

            // Own team's tactic fields must be restored after the suggestion search.
            Assert.Equal(PlayingStyle.CounterAttack, own.PlayingStyle);
            Assert.Equal(TacticalOrientation.Balanced, own.TacticalOrientation);
        }

        [Fact]
        public void AnalysisAbility89_DoesNotRevealFormationOrLineup()
        {
            var own = TestHelpers.CreateTeam("Own", baseRating: 60);
            var opponent = WeakDefenseOpponent(id: 2);
            var standings = new List<StandingRow> { OpponentRow(2) };
            var leagueTeams = new List<Team> { opponent, TestHelpers.CreateTeam("Avg", baseRating: 60) };

            var report = ScoutingReportService.BuildReport(own, opponent, standings, leagueTeams, analysisAbility: 89);

            Assert.Null(report.OpponentFormationName);
            Assert.Null(report.OpponentPlayingStyle);
            Assert.Null(report.OpponentTacticalOrientation);
            Assert.Null(report.OpponentStartingXINames);
        }

        [Fact]
        public void AnalysisAbility90_RevealsFormationPlayingStyleOrientationAndStartingXI()
        {
            var own = TestHelpers.CreateTeam("Own", baseRating: 60);
            var opponent = WeakDefenseOpponent(id: 2);
            opponent.FormationName = "4-4-2";
            var standings = new List<StandingRow> { OpponentRow(2) };
            var leagueTeams = new List<Team> { opponent, TestHelpers.CreateTeam("Avg", baseRating: 60) };

            var report = ScoutingReportService.BuildReport(own, opponent, standings, leagueTeams, analysisAbility: 90);

            Assert.Equal("4-4-2", report.OpponentFormationName);
            Assert.Equal(opponent.PlayingStyle, report.OpponentPlayingStyle);
            Assert.Equal(opponent.TacticalOrientation, report.OpponentTacticalOrientation);
            Assert.NotNull(report.OpponentStartingXINames);
            Assert.Equal(11, report.OpponentStartingXINames!.Count);
        }
    }
}
