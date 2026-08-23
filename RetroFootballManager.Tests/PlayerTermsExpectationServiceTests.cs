using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class PlayerTermsExpectationServiceTests
    {
        private static Player MakePlayer(
            double rating = 60, int talent = 50, int age = 25, int moral = 50, bool wantsToLeave = false) =>
            new() { Rating = rating, Talent = talent, Age = age, Moral = moral, WantsToLeaveClub = wantsToLeave };

        [Fact]
        public void EstimateExpectedRole_StrongPlayer_ExpectsKeyPlayer()
        {
            var player = MakePlayer(rating: 75, age: 27, talent: 50);
            Assert.Equal(RoleInTeam.KeyPlayer, PlayerTermsExpectationService.EstimateExpectedRole(player, squadAverageRating: 60));
        }

        [Fact]
        public void EstimateExpectedRole_YoungHighTalent_ExpectsFutureTalent()
        {
            var player = MakePlayer(rating: 55, age: 19, talent: 85);
            Assert.Equal(RoleInTeam.FutureTalent, PlayerTermsExpectationService.EstimateExpectedRole(player, squadAverageRating: 60));
        }

        [Fact]
        public void EstimateExpectedRole_WeakVeteran_ExpectsBackup()
        {
            var player = MakePlayer(rating: 45, age: 34, talent: 30);
            Assert.Equal(RoleInTeam.Backup, PlayerTermsExpectationService.EstimateExpectedRole(player, squadAverageRating: 60));
        }

        [Theory]
        [InlineData(20, 4)]
        [InlineData(27, 3)]
        [InlineData(31, 2)]
        [InlineData(36, 1)]
        public void EstimatePreferredContractYears_ScalesDownWithAge(int age, int expectedYears)
        {
            Assert.Equal(expectedYears, PlayerTermsExpectationService.EstimatePreferredContractYears(age));
        }

        [Fact]
        public void EstimateSatisfaction_MatchingOffer_ScoresHigherThanLowballOffer()
        {
            var player = MakePlayer(rating: 60, age: 27, talent: 50);
            double expectedWage = PlayerTermsExpectationService.EstimateExpectedWage(player);
            var expectedRole = PlayerTermsExpectationService.EstimateExpectedRole(player, squadAverageRating: 60);
            int preferredYears = PlayerTermsExpectationService.EstimatePreferredContractYears(player.Age);

            double goodOffer = PlayerTermsExpectationService.EstimateSatisfaction(
                player, squadAverageRating: 60, offeredWage: expectedWage, offeredRole: expectedRole,
                offeredContractYears: preferredYears, hasExitClause: false, totalAnnualBonusValue: 0);

            double lowballOffer = PlayerTermsExpectationService.EstimateSatisfaction(
                player, squadAverageRating: 60, offeredWage: expectedWage * 0.3, offeredRole: RoleInTeam.Backup,
                offeredContractYears: 1, hasExitClause: false, totalAnnualBonusValue: 0);

            Assert.True(goodOffer > lowballOffer);
        }

        [Fact]
        public void EstimateSatisfaction_AmbitiousPlayer_PenalizedByMissingExitClause()
        {
            var ambitiousPlayer = MakePlayer(rating: 60, age: 27, talent: 90);
            double expectedWage = PlayerTermsExpectationService.EstimateExpectedWage(ambitiousPlayer);

            double withClause = PlayerTermsExpectationService.EstimateSatisfaction(
                ambitiousPlayer, squadAverageRating: 60, offeredWage: expectedWage, offeredRole: RoleInTeam.KeyPlayer,
                offeredContractYears: 3, hasExitClause: true, totalAnnualBonusValue: 0);

            double withoutClause = PlayerTermsExpectationService.EstimateSatisfaction(
                ambitiousPlayer, squadAverageRating: 60, offeredWage: expectedWage, offeredRole: RoleInTeam.KeyPlayer,
                offeredContractYears: 3, hasExitClause: false, totalAnnualBonusValue: 0);

            Assert.True(withClause > withoutClause);
        }

        [Theory]
        [InlineData(95, NegotiationMoodLevel.Delighted)]
        [InlineData(75, NegotiationMoodLevel.Happy)]
        [InlineData(55, NegotiationMoodLevel.Neutral)]
        [InlineData(35, NegotiationMoodLevel.Impatient)]
        [InlineData(10, NegotiationMoodLevel.Furious)]
        public void EvaluateMood_MapsSatisfactionToExpectedTier(double satisfaction, NegotiationMoodLevel expected)
        {
            Assert.Equal(expected, PlayerTermsExpectationService.EvaluateMood(satisfaction));
        }

        [Fact]
        public void IsWillingToDiscussRenewal_RefusesWhenPlayerWantsToLeave()
        {
            var player = MakePlayer(rating: 70, age: 27, wantsToLeave: true);
            Assert.False(PlayerTermsExpectationService.IsWillingToDiscussRenewal(player, squadAverageRating: 60));
        }

        [Fact]
        public void IsWillingToDiscussRenewal_RefusesWhenMoraleTooLow()
        {
            var player = MakePlayer(rating: 70, age: 27, moral: 10);
            Assert.False(PlayerTermsExpectationService.IsWillingToDiscussRenewal(player, squadAverageRating: 60));
        }

        [Fact]
        public void IsWillingToDiscussRenewal_AcceptsGoodStandingPlayer()
        {
            var player = MakePlayer(rating: 65, age: 27, moral: 60);
            Assert.True(PlayerTermsExpectationService.IsWillingToDiscussRenewal(player, squadAverageRating: 60));
        }
    }
}
