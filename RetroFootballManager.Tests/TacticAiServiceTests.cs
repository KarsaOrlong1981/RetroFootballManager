using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class TacticAiServiceTests
    {
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

        // Ein Random, dessen erster NextDouble()-Wert garantiert unter jeder ActivityChance
        // liegt (0.0), damit die KI in Tests deterministisch aktiv wird.
        private sealed class AlwaysZeroRandom : Random
        {
            public override double NextDouble() => 0.0;
        }

        [Fact]
        public void ApplyPreMatchTactic_SwitchesToMoreOffensiveTactic_AgainstWeakDefense()
        {
            var aiTeam = TestHelpers.CreateTeam(
                "KI", baseRating: 60, style: PlayingStyle.CounterAttack, orientation: TacticalOrientation.Balanced);
            var opponent = WeakDefenseOpponent(id: 2);
            var standings = new List<StandingRow>
            {
                new(5, 2, "Opponent", 10, 4, 2, 4, 15, 14, 1, 14, "WWDLL"),
            };
            var leagueTeams = new List<Team>
            {
                opponent, TestHelpers.CreateTeam("Avg1", baseRating: 60), TestHelpers.CreateTeam("Avg2", baseRating: 60),
            };

            bool changed = TacticAiService.ApplyPreMatchTactic(
                aiTeam, opponent, standings, leagueTeams, Difficulty.Hard, new AlwaysZeroRandom());

            Assert.True(changed);
        }

        [Fact]
        public void ApplyPreMatchTactic_LowActivityChance_LeavesTacticUnchanged()
        {
            var aiTeam = TestHelpers.CreateTeam(
                "KI", baseRating: 60, style: PlayingStyle.CounterAttack, orientation: TacticalOrientation.Balanced);
            var opponent = WeakDefenseOpponent(id: 2);
            var standings = new List<StandingRow> { new(5, 2, "Opponent", 10, 4, 2, 4, 15, 14, 1, 14, "WWDLL") };
            var leagueTeams = new List<Team> { opponent };

            // NextDouble() liefert immer 1.0 (Random.Shared.NextDouble() Obergrenze niemals
            // erreicht, aber ein handgestricktes "immer 1.0" reicht, um jede ActivityChance zu
            // unterschreiten... genauer: zu ÜBERSCHREITEN, sodass RunWeeklyTickAsync abbricht).
            var neverActive = new AlwaysOneRandom();

            bool changed = TacticAiService.ApplyPreMatchTactic(
                aiTeam, opponent, standings, leagueTeams, Difficulty.Easy, neverActive);

            Assert.False(changed);
            Assert.Equal(PlayingStyle.CounterAttack, aiTeam.PlayingStyle);
            Assert.Equal(TacticalOrientation.Balanced, aiTeam.TacticalOrientation);
        }

        private sealed class AlwaysOneRandom : Random
        {
            public override double NextDouble() => 1.0;
        }
    }
}
