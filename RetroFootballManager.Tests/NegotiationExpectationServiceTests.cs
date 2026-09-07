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

        [Fact]
        public void EstimateLevelGapPremium_IsZero_WhenBuyerIsNotInAWeakerLeague()
        {
            var elitePlayer = MakePlayer(talent: 90, age: 26);
            elitePlayer.Rating = 90;

            Assert.Equal(0, NegotiationExpectationService.EstimateLevelGapPremium(elitePlayer, sellingTeamTier: 2, buyingTeamTier: 2));
            Assert.Equal(0, NegotiationExpectationService.EstimateLevelGapPremium(elitePlayer, sellingTeamTier: 2, buyingTeamTier: 1));
        }

        [Fact]
        public void EstimateLevelGapPremium_IsZero_ForAnAverageMoverEvenAcrossManyTiers()
        {
            var averagePlayer = MakePlayer(talent: 40, age: 26);
            averagePlayer.Rating = 60;

            Assert.Equal(0, NegotiationExpectationService.EstimateLevelGapPremium(averagePlayer, sellingTeamTier: 1, buyingTeamTier: 4));
        }

        [Fact]
        public void EstimateLevelGapPremium_GrowsWithTierGapAndPlayerQuality_ForATopFlightMoveDown()
        {
            var elitePlayer = MakePlayer(talent: 95, age: 26);
            elitePlayer.Rating = 90;

            var oneTierDown = NegotiationExpectationService.EstimateLevelGapPremium(elitePlayer, sellingTeamTier: 1, buyingTeamTier: 2);
            var threeTiersDown = NegotiationExpectationService.EstimateLevelGapPremium(elitePlayer, sellingTeamTier: 1, buyingTeamTier: 4);

            Assert.True(oneTierDown > 0);
            Assert.True(threeTiersDown > oneTierDown);
        }

        [Fact]
        public void EstimateExpectedFee_IsMuchHigher_WhenBuyerIsAFarWeakerLeagueThanSeller()
        {
            var elitePlayer = MakePlayer(talent: 95, age: 26);
            elitePlayer.Rating = 92;
            double baseFee = 1_000_000;

            var sameLevel = NegotiationExpectationService.EstimateExpectedFee(baseFee, elitePlayer, seasonStats: null, sellingTeamTier: 1, buyingTeamTier: 1);
            var farWeakerBuyer = NegotiationExpectationService.EstimateExpectedFee(baseFee, elitePlayer, seasonStats: null, sellingTeamTier: 1, buyingTeamTier: 4);

            Assert.True(farWeakerBuyer > sameLevel * 2);
        }
    }
}
