using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class PositionSkillTests
    {
        [Fact]
        public void NaturalPosition_HasNoMalus()
        {
            var player = new Player { Position = Position.CentralMidfielder };

            Assert.Equal(1.0, PositionSkillEffects.GetMultiplier(player));
        }

        [Fact]
        public void AssignedToNaturalPosition_HasNoMalus()
        {
            var player = new Player
            {
                Position = Position.CentralMidfielder,
                AssignedPosition = Position.CentralMidfielder,
            };

            Assert.Equal(1.0, PositionSkillEffects.GetMultiplier(player));
        }

        [Fact]
        public void ListedSecondaryPosition_HasModerateMalus_ScaledByProficiency()
        {
            var lowProficiency = new Player
            {
                Position = Position.CentralDefender,
                AssignedPosition = Position.LeftDefender,
                SecondaryPositions = [new PositionSkill(Position.LeftDefender, 30)],
            };

            var highProficiency = new Player
            {
                Position = Position.CentralDefender,
                AssignedPosition = Position.LeftDefender,
                SecondaryPositions = [new PositionSkill(Position.LeftDefender, 90)],
            };

            double lowMultiplier = PositionSkillEffects.GetMultiplier(lowProficiency);
            double highMultiplier = PositionSkillEffects.GetMultiplier(highProficiency);

            Assert.True(lowMultiplier < 1.0);
            Assert.True(highMultiplier < 1.0);
            Assert.True(highMultiplier > lowMultiplier, "Höhere Proficiency soll einen geringeren Malus bedeuten.");
        }

        [Fact]
        public void UnlistedPosition_HasSeverelyHigherMalusThanListedSecondary()
        {
            var withSecondary = new Player
            {
                Position = Position.CentralDefender,
                AssignedPosition = Position.LeftDefender,
                SecondaryPositions = [new PositionSkill(Position.LeftDefender, 50)],
            };

            var outOfPosition = new Player
            {
                Position = Position.CentralDefender,
                AssignedPosition = Position.Forward,
                SecondaryPositions = [new PositionSkill(Position.LeftDefender, 50)],
            };

            double listedMultiplier = PositionSkillEffects.GetMultiplier(withSecondary);
            double unlistedMultiplier = PositionSkillEffects.GetMultiplier(outOfPosition);

            Assert.True(unlistedMultiplier < listedMultiplier);
            Assert.True(unlistedMultiplier < 0.5, "Eine komplett fremde Position soll einen sehr hohen Malus haben.");
        }

        [Fact]
        public void SecondaryPositions_PersistsAsJson_RoundTrips()
        {
            var player = new Player
            {
                SecondaryPositions =
                [
                    new PositionSkill(Position.LeftDefender, 70),
                    new PositionSkill(Position.RightDefender, 60),
                ],
            };

            var reloaded = new Player { SecondaryPositionsRaw = player.SecondaryPositionsRaw };

            Assert.Equal(2, reloaded.SecondaryPositions.Count);
            Assert.Contains(reloaded.SecondaryPositions, s => s.Position == Position.LeftDefender && s.Proficiency == 70);
            Assert.Contains(reloaded.SecondaryPositions, s => s.Position == Position.RightDefender && s.Proficiency == 60);
        }

        [Fact]
        public void Simulate_OutOfPositionPlayer_WeakensTeamOverall()
        {
            var inPosition = TestHelpers.CreateTeam("InPosition", baseRating: 70);

            var outOfPosition = TestHelpers.CreateTeam("OutOfPosition", baseRating: 70);
            foreach (var p in outOfPosition.Players.Where(p => p.Position == Position.Forward))
                p.AssignedPosition = Position.CentralDefender;

            var inPositionProfile = TeamStrengthCalculator.Calculate(inPosition, isHome: false);
            var outOfPositionProfile = TeamStrengthCalculator.Calculate(outOfPosition, isHome: false);

            Assert.True(outOfPositionProfile.Overall < inPositionProfile.Overall);
        }

        [Fact]
        public void UsedAsWingBack_TrueWithCorrectAssignedPosition_GetsHisWingBackProficiencyNotABaseRoleMalus()
        {
            // Regression test: Player.UsedAsWingBack alone doesn't drive the match engine at
            // all - only AssignedPosition/EffectivePosition does (PositionSkillEffects.GetMultiplier
            // reads player.EffectivePosition). This test proves that when the flag is derived into
            // AssignedPosition the same way LineupViewModel.ApplyLineup/EffectiveSlotPositionFor
            // do it (slot's AlternateRole wins whenever UsedAsWingBack is true), a player who is
            // genuinely good at RightWingBack (high-proficiency secondary position) gets evaluated
            // via THAT proficiency - not treated as an out-of-position plain full-back, which would
            // wrongly tank the team's strength for a manager who correctly identified a good WB.
            var slot = new FormationSlot(Position.RightDefender, 0.82, 0.72, Position.RightWingBack);

            var goodWingBack = new Player
            {
                Position = Position.RightDefender,
                UsedAsWingBack = true,
                SecondaryPositions = [new PositionSkill(Position.RightWingBack, 90)],
            };
            // Mirrors LineupViewModel.EffectiveSlotPositionFor + ApplyLineup exactly.
            var slotPos = slot.AlternateRole is Position alt && goodWingBack.UsedAsWingBack ? alt : slot.Position;
            goodWingBack.AssignedPosition = slotPos == goodWingBack.Position ? null : slotPos;

            var noAbilityWingBack = new Player
            {
                Position = Position.RightDefender,
                UsedAsWingBack = true,
                SecondaryPositions = [], // no WB ability at all
            };
            var noAbilitySlotPos = slot.AlternateRole is Position alt2 && noAbilityWingBack.UsedAsWingBack ? alt2 : slot.Position;
            noAbilityWingBack.AssignedPosition = noAbilitySlotPos == noAbilityWingBack.Position ? null : noAbilitySlotPos;

            Assert.Equal(Position.RightWingBack, goodWingBack.EffectivePosition);
            double goodMultiplier = PositionSkillEffects.GetMultiplier(goodWingBack);
            double noAbilityMultiplier = PositionSkillEffects.GetMultiplier(noAbilityWingBack);

            Assert.True(goodMultiplier > 0.85, "Ein Spieler mit hoher RAV-Eignung darf keinen harten Fremdpositions-Malus bekommen.");
            Assert.True(goodMultiplier > noAbilityMultiplier,
                "Ein WB-fähiger Spieler muss besser bewertet werden als einer ohne jede WB-Eignung, sonst ist der Toggle nutzlos/schädlich.");
        }

        [Fact]
        public void GetGoalkeeper_FindsPlayerAssignedToGoalkeeperEvenIfNotNaturalPosition()
        {
            var team = TestHelpers.CreateTeam("Team", baseRating: 65);
            var naturalKeeper = team.Players.Single(p => p.Position == Position.Goalkeeper);
            naturalKeeper.AssignedPosition = Position.Forward;

            var emergencyKeeper = team.Players.First(p => p.Position == Position.CentralDefender);
            emergencyKeeper.AssignedPosition = Position.Goalkeeper;

            var goalkeeper = TeamStrengthCalculator.GetGoalkeeper(team);

            Assert.Equal(emergencyKeeper.Id, goalkeeper?.Id);
        }
    }
}
