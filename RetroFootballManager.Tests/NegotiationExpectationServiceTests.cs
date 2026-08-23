using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class NegotiationExpectationServiceTests
    {
        private static Player MakePlayer(int talent, int age) => new() { Talent = talent, Age = age };

        private static PlayerStats MakeStats(int appearances, double rating) =>
            new() { Appearances = appearances, Rating = rating };

        [Fact]
        public void EstimatePerformancePremium_IsZero_ForAverageVeteran()
        {
            var player = MakePlayer(talent: 40, age: 30);
            Assert.Equal(0, NegotiationExpectationService.EstimatePerformancePremium(player, seasonStats: null));
        }

        [Fact]
        public void EstimatePerformancePremium_IsCapped_AtMax()
        {
            var player = MakePlayer(talent: 99, age: 18);
            var stats = MakeStats(appearances: 30, rating: 1.5);

            var premium = NegotiationExpectationService.EstimatePerformancePremium(player, stats);

            Assert.Equal(NegotiationExpectationService.MaxPerformancePremium, premium);
        }

        [Fact]
        public void EstimatePerformancePremium_RewardsYoungTalentWithGoodStats_MoreThanTalentAlone()
        {
            var talentOnly = MakePlayer(talent: 88, age: 22);
            var talentWithForm = MakePlayer(talent: 88, age: 22);
            var goodStats = MakeStats(appearances: 20, rating: 2.5);

            var withoutStats = NegotiationExpectationService.EstimatePerformancePremium(talentOnly, seasonStats: null);
            var withStats = NegotiationExpectationService.EstimatePerformancePremium(talentWithForm, goodStats);

            Assert.True(withStats > withoutStats);
        }

        [Fact]
        public void EstimateExpectedFee_AppliesPremiumOnTopOfBaseFee()
        {
            var player = MakePlayer(talent: 90, age: 20);
            double baseFee = 1_000_000;

            var expected = NegotiationExpectationService.EstimateExpectedFee(baseFee, player, seasonStats: null);

            Assert.True(expected > baseFee);
        }

        [Theory]
        [InlineData(1.2, NegotiationMoodLevel.Delighted)]
        [InlineData(1.05, NegotiationMoodLevel.Happy)]
        [InlineData(0.9, NegotiationMoodLevel.Neutral)]
        [InlineData(0.75, NegotiationMoodLevel.Impatient)]
        [InlineData(0.5, NegotiationMoodLevel.Furious)]
        public void EvaluateFeeMood_MapsRatioToExpectedTier(double ratio, NegotiationMoodLevel expected)
        {
            Assert.Equal(expected, NegotiationExpectationService.EvaluateFeeMood(ratio));
        }

        [Fact]
        public void EvaluateFeeMood_ImprovingOffer_RecoversMoodFromFurious()
        {
            var lowBallMood = NegotiationExpectationService.EvaluateFeeMood(0.5);
            var improvedMood = NegotiationExpectationService.EvaluateFeeMood(1.1);

            Assert.Equal(NegotiationMoodLevel.Furious, lowBallMood);
            Assert.True(improvedMood > lowBallMood);
        }
    }
}
