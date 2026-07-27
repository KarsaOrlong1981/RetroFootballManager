using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class TacticTests
    {
        // Regressionstest: jeder Spielstil UND jede Ausrichtung müssen ALLE neun
        // Attributfaktoren setzen. Ein vergessener Faktor bleibt sonst beim C#-Default 0.0
        // und entfernt das betroffene Spielerattribut faktisch komplett aus der
        // Stärkeberechnung (siehe historischer Pressing-Bug: Offense/Intelligence/Counter/
        // Efficiency waren 0.0, wodurch ein 88er-Team gegen ein 48er-Team kaum noch angreifen
        // konnte).
        [Theory]
        [InlineData(PlayingStyle.CounterAttack)]
        [InlineData(PlayingStyle.TikiTaka)]
        [InlineData(PlayingStyle.Pressing)]
        [InlineData(PlayingStyle.WingPlay)]
        [InlineData(PlayingStyle.CrossesToStriker)]
        public void Tactic_NeverZeroesOutAFactor_AcrossAllStyles(PlayingStyle style)
        {
            var tactic = new Tactic(style, TacticalOrientation.Balanced);
            AssertAllFactorsPositive(tactic);
        }

        [Theory]
        [InlineData(TacticalOrientation.VeryDefensive)]
        [InlineData(TacticalOrientation.Defensive)]
        [InlineData(TacticalOrientation.Balanced)]
        [InlineData(TacticalOrientation.Offensive)]
        [InlineData(TacticalOrientation.VeryOffensive)]
        public void Tactic_NeverZeroesOutAFactor_AcrossAllOrientations(TacticalOrientation orientation)
        {
            var tactic = new Tactic(PlayingStyle.CounterAttack, orientation);
            AssertAllFactorsPositive(tactic);
        }

        private static void AssertAllFactorsPositive(Tactic tactic)
        {
            Assert.True(tactic.OffensivePowerFactor > 0);
            Assert.True(tactic.DefensivePowerFactor > 0);
            Assert.True(tactic.GameIntelligenceFactor > 0);
            Assert.True(tactic.PressingIntensityFactor > 0);
            Assert.True(tactic.CounterSpeedFactor > 0);
            Assert.True(tactic.PassingAccuracyFactor > 0);
            Assert.True(tactic.DuelHardnessFactor > 0);
            Assert.True(tactic.DuelEfficiencyFactor > 0);
            Assert.True(tactic.CrossingAccuracyFactor > 0);
        }

        [Fact]
        public void PressingStyle_DoesNotCrushAttackingStrength()
        {
            // Reproduziert das gemeldete Szenario: ein 88er-Pressing-Team muss gegen
            // ein 48er-Konter-Team klar im Angriffswert überlegen sein.
            var strong = TestHelpers.CreateTeam("Stark", baseRating: 88, style: PlayingStyle.Pressing);
            var weak = TestHelpers.CreateTeam("Schwach", baseRating: 48, style: PlayingStyle.CounterAttack);

            var strongProfile = TeamStrengthCalculator.Calculate(strong, isHome: true);
            var weakProfile = TeamStrengthCalculator.Calculate(weak, isHome: false);

            Assert.True(strongProfile.Attack > weakProfile.Attack * 1.3);
            Assert.True(strongProfile.Overall > weakProfile.Overall * 1.3);
        }

        [Fact]
        public void VeryOffensiveOrientation_IncreasesAttack_ComparedToVeryDefensive()
        {
            var offensive = TestHelpers.CreateTeam("Offensiv", baseRating: 60, orientation: TacticalOrientation.VeryOffensive);
            var defensive = TestHelpers.CreateTeam("Defensiv", baseRating: 60, orientation: TacticalOrientation.VeryDefensive);

            var offensiveProfile = TeamStrengthCalculator.Calculate(offensive, isHome: false);
            var defensiveProfile = TeamStrengthCalculator.Calculate(defensive, isHome: false);

            Assert.True(offensiveProfile.Attack > defensiveProfile.Attack);
            Assert.True(offensiveProfile.Defense < defensiveProfile.Defense);
        }
    }
}
