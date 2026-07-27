using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class PlayerValuationServiceTests
    {
        private static Player MakePlayer(double rating, int talent = 50, int age = 25) =>
            new() { Rating = rating, Talent = talent, Age = age };

        [Theory]
        [InlineData(30)]
        [InlineData(45)]
        [InlineData(60)]
        public void RatingUpTo60_StaysUnder1Million(double rating)
        {
            var value = PlayerValuationService.EstimateMarketValue(MakePlayer(rating, talent: 99, age: 25));
            Assert.True(value < 1_000_000, $"Rating {rating} ergab {value}");
        }

        [Theory]
        [InlineData(65)]
        [InlineData(70)]
        public void RatingBetween60And70_StaysUnder4Million(double rating)
        {
            var value = PlayerValuationService.EstimateMarketValue(MakePlayer(rating, talent: 99, age: 25));
            Assert.True(value < 4_000_000, $"Rating {rating} ergab {value}");
        }

        [Theory]
        [InlineData(75)]
        [InlineData(80)]
        public void RatingBetween70And80_StaysBetween4And20Million(double rating)
        {
            var value = PlayerValuationService.EstimateMarketValue(MakePlayer(rating, talent: 99, age: 25));
            Assert.InRange(value, 3_900_000, 20_000_000);
        }

        [Fact]
        public void RatingAbove80_CanExceed20Million()
        {
            var value = PlayerValuationService.EstimateMarketValue(MakePlayer(95, talent: 90, age: 26));
            Assert.True(value > 20_000_000);
        }

        [Fact]
        public void HigherTalent_IncreasesValue_ForSameRatingAndAge()
        {
            var lowTalent = PlayerValuationService.EstimateMarketValue(MakePlayer(85, talent: 10, age: 26));
            var highTalent = PlayerValuationService.EstimateMarketValue(MakePlayer(85, talent: 90, age: 26));

            Assert.True(highTalent > lowTalent);
        }

        [Fact]
        public void OlderPlayer_IsWorthLess_ThanPeakAgePlayer()
        {
            var peakAge = PlayerValuationService.EstimateMarketValue(MakePlayer(85, talent: 50, age: 27));
            var veteran = PlayerValuationService.EstimateMarketValue(MakePlayer(85, talent: 50, age: 36));

            Assert.True(veteran < peakAge);
        }

        [Fact]
        public void EstimateAnnualSalary_IsFractionOfMarketValue()
        {
            var player = MakePlayer(70, talent: 50, age: 27);
            var marketValue = PlayerValuationService.EstimateMarketValue(player);
            var salary = PlayerValuationService.EstimateAnnualSalary(player);

            Assert.Equal(Math.Round(marketValue * 0.15), salary);
        }
    }
}
