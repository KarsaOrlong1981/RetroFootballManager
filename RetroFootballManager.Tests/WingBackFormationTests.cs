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
        public void BackThreeFormation_HasNoWingBackAlternate()
        {
            // The AV/WB toggle only ever applies to LV/RV slots. A back-three has no full-back
            // slots at all, so 3-5-2 simply has no wing-back alternate anywhere - not even on
            // its wide midfielders.
            Assert.All(FormationCatalog.F352.Slots, s => Assert.Null(s.AlternateRole));
        }

        [Fact]
        public void EveryBackFourFormation_HasExactlyTwoWingBackAlternates()
        {
            foreach (var formation in FormationCatalog.All.Where(f => f.Name != FormationCatalog.F352.Name))
            {
                int count = formation.Slots.Count(s => s.AlternateRole is Position.LeftWingBack or Position.RightWingBack);
                Assert.Equal(2, count);
            }
        }

        [Fact]
        public void WingBackAlternate_OnlyEverAppliesToFullBackSlots()
        {
            foreach (var formation in FormationCatalog.All)
            {
                var withAlternate = formation.Slots.Where(s => s.AlternateRole is not null);
                Assert.All(withAlternate, s => Assert.True(s.Position is Position.LeftDefender or Position.RightDefender));
            }
        }
    }
}
