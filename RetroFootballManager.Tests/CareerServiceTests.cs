using RetroFootballManager.Common;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class CareerServiceTests : IDisposable
    {
        private readonly string _path;

        public CareerServiceTests()
        {
            _path = Path.Combine(Path.GetTempPath(), $"rfm_career_{Guid.NewGuid():N}.json");
        }

        public void Dispose()
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }

        [Fact]
        public void FreshProfile_OnlyTier4Unlocked()
        {
            var service = new CareerService(_path);
            Assert.Equal(4, service.HighestUnlockedTier);
            Assert.True(service.IsTierUnlocked(4));
            Assert.False(service.IsTierUnlocked(3));
            Assert.Equal(CareerService.Tier3Threshold, service.PointsToNextTier());
        }

        [Theory]
        [InlineData(50, 4)]
        [InlineData(100, 3)]
        [InlineData(299, 3)]
        [InlineData(300, 2)]
        [InlineData(700, 1)]
        [InlineData(1000, 1)]
        public void HighestUnlockedTier_FollowsThresholds(int points, int expectedTier)
        {
            var service = new CareerService(_path);
            service.AwardPoints(season: 1, "test", points);
            Assert.Equal(expectedTier, service.HighestUnlockedTier);
        }

        [Fact]
        public void Points_PersistAcrossServiceInstances()
        {
            var first = new CareerService(_path);
            first.AwardPoints(season: 1, "Aufstieg", 100);

            var second = new CareerService(_path);
            Assert.Equal(100, second.Points);
            Assert.True(second.IsTierUnlocked(3));
        }
    }
}
