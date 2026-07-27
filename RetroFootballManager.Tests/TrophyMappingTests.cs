using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class TrophyMappingTests
    {
        [Theory]
        [InlineData(1, TrophyType.DeutscherMeister)]
        [InlineData(2, TrophyType.MeisterLiga2)]
        [InlineData(3, TrophyType.MeisterLiga3)]
        [InlineData(4, TrophyType.MeisterLiga4)]
        public void FromLeagueTier_MapsCorrectly(int tier, TrophyType expected)
        {
            Assert.Equal(expected, TrophyMapping.FromLeagueTier(tier));
        }

        [Fact]
        public void FromLeagueTier_ThrowsForOutOfRangeTier()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TrophyMapping.FromLeagueTier(5));
        }

        [Theory]
        [InlineData(CompetitionType.GermanCup, TrophyType.DeutscherPokal)]
        [InlineData(CompetitionType.ChampionsLeague, TrophyType.EuropaPokalDerMeister)]
        [InlineData(CompetitionType.EuropaCup, TrophyType.EuropaPokal)]
        public void FromCompetition_MapsCorrectly(CompetitionType competition, TrophyType expected)
        {
            Assert.Equal(expected, TrophyMapping.FromCompetition(competition));
        }
    }
}
