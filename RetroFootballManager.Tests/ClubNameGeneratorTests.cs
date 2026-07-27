using RetroFootballManager.Common;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class ClubNameGeneratorTests
    {
        [Fact]
        public void FixedRoster_Has72Clubs_18PerTier()
        {
            var roster = ClubNameGenerator.FixedRoster;

            Assert.Equal(72, roster.Count);
            foreach (var tier in Enumerable.Range(1, 4))
                Assert.Equal(18, roster.Count(c => c.Tier == tier));
        }

        [Fact]
        public void FixedRoster_HasNoDuplicateNames_AndIsStableAcrossAccesses()
        {
            var roster = ClubNameGenerator.FixedRoster;

            Assert.Equal(roster.Count, roster.Select(c => c.Name).Distinct().Count());

            // Accessing the roster again must yield the exact same, deterministic list.
            var again = ClubNameGenerator.FixedRoster;
            Assert.Equal(roster, again);
        }
    }
}
