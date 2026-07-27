using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class WingBackFormationTests
    {
        [Fact]
        public void BackFourFormations_OfferWingBackAlternateOnFullBackSlots()
        {
            foreach (var formation in new[] { FormationCatalog.F442, FormationCatalog.F433, FormationCatalog.F4231 })
            {
                var left = formation.Slots.Single(s => s.Position == Position.LeftDefender);
                var right = formation.Slots.Single(s => s.Position == Position.RightDefender);

                Assert.Equal(Position.LeftWingBack, left.AlternateRole);
                Assert.Equal(Position.RightWingBack, right.AlternateRole);
            }
        }

        [Fact]
        public void BackThreeFormation_OffersWingBackAlternateOnWideMidfieldSlots()
        {
            var left = FormationCatalog.F352.Slots.Single(s => s.Position == Position.LeftMidfielder);
            var right = FormationCatalog.F352.Slots.Single(s => s.Position == Position.RightMidfielder);

            Assert.Equal(Position.LeftWingBack, left.AlternateRole);
            Assert.Equal(Position.RightWingBack, right.AlternateRole);

            // No wide full-back slots exist in a back-three, so the defenders don't get one.
            var defenders = FormationCatalog.F352.Slots.Where(s =>
                s.Position is Position.LeftDefender or Position.RightDefender);
            Assert.All(defenders, d => Assert.Null(d.AlternateRole));
        }

        [Fact]
        public void EveryFormation_HasExactlyTwoWingBackAlternates()
        {
            foreach (var formation in FormationCatalog.All)
            {
                int count = formation.Slots.Count(s => s.AlternateRole is Position.LeftWingBack or Position.RightWingBack);
                Assert.Equal(2, count);
            }
        }
    }
}
