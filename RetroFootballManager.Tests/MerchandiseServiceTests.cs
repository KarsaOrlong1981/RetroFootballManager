using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class MerchandiseServiceTests
    {
        private static Team Team(int tier = 2, int balance = 100_000, int clubMembers = 50_000, bool withDirectorOfFootball = false)
        {
            var team = TestHelpers.CreateTeam("Test-Verein", baseRating: 60);
            team.LeagueTier = tier;
            team.Finances = new Finances { CurrentBalance = balance, ClubMembers = clubMembers };
            if (withDirectorOfFootball)
                team.Employees.Add(new Employee { EmployeeType = EmployeeType.DirectorOfFootball, Rating = 70 });
            return team;
        }

        private static MerchandiseArticleStock Stock(MerchandiseArticleType type) =>
            new() { Type = type, Stock = 1000, SellPrice = MerchandiseCatalog.Get(type).DefaultSellPrice };

        [Fact]
        public void CalculateWeeklySale_StarJersey_OutsellsGenericJersey_WhenBestPlayerIsGood()
        {
            var fanJersey = Stock(MerchandiseArticleType.FanTrikot);
            var starJersey = Stock(MerchandiseArticleType.StarSpielerTrikot);

            var fanSale = MerchandiseSalesCalculator.CalculateWeeklySale(fanJersey, 50_000, performanceFactor: 1.0, DateTime.Today);
            var starSale = MerchandiseSalesCalculator.CalculateWeeklySale(starJersey, 50_000, performanceFactor: 1.0, DateTime.Today, bestPlayerRating: 90);

            Assert.True(starSale.UnitsSold > fanSale.UnitsSold);
        }

        [Fact]
        public void CalculateWeeklySale_StarJersey_WithAverageBestPlayer_DoesNotOutsellGenericJersey()
        {
            var starJersey = Stock(MerchandiseArticleType.StarSpielerTrikot);

            var neutralSale = MerchandiseSalesCalculator.CalculateWeeklySale(starJersey, 50_000, performanceFactor: 1.0, DateTime.Today, bestPlayerRating: 50);
            var fanJersey = Stock(MerchandiseArticleType.FanTrikot);
            var fanSale = MerchandiseSalesCalculator.CalculateWeeklySale(fanJersey, 50_000, performanceFactor: 1.0, DateTime.Today);

            Assert.True(neutralSale.UnitsSold <= fanSale.UnitsSold);
        }

        [Fact]
        public void CalculateWeeklySale_HigherPriceThanReference_SellsFewerUnits()
        {
            var cheap = Stock(MerchandiseArticleType.Tasse);
            var expensive = Stock(MerchandiseArticleType.Tasse);
            expensive.SellPrice *= 3;

            var cheapSale = MerchandiseSalesCalculator.CalculateWeeklySale(cheap, 50_000, performanceFactor: 1.0, DateTime.Today);
            var expensiveSale = MerchandiseSalesCalculator.CalculateWeeklySale(expensive, 50_000, performanceFactor: 1.0, DateTime.Today);

            Assert.True(expensiveSale.UnitsSold < cheapSale.UnitsSold);
        }

        [Fact]
        public void SeasonalFactor_WinterArticle_SellsBetterInWinterThanSummer()
        {
            double winterMonth = MerchandiseSalesCalculator.SeasonalFactor(MerchandiseSeason.Winter, new DateTime(2026, 1, 15));
            double summerMonth = MerchandiseSalesCalculator.SeasonalFactor(MerchandiseSeason.Winter, new DateTime(2026, 7, 15));

            Assert.True(winterMonth > summerMonth);
        }

        [Fact]
        public void SeasonalFactor_AllYearArticle_IsAlwaysNeutral()
        {
            Assert.Equal(1.0, MerchandiseSalesCalculator.SeasonalFactor(MerchandiseSeason.AllYear, new DateTime(2026, 1, 15)));
            Assert.Equal(1.0, MerchandiseSalesCalculator.SeasonalFactor(MerchandiseSeason.AllYear, new DateTime(2026, 7, 15)));
        }

        [Fact]
        public void PerformanceFactor_BetterTablePositionAndForm_YieldsHigherFactor()
        {
            var strugglingRow = new StandingRow(18, 1, "Test", 10, 1, 1, 8, 5, 20, -15, 4, "LLLLL");
            var leadingRow = new StandingRow(1, 1, "Test", 10, 8, 1, 1, 20, 5, 15, 25, "WWWWW");

            double low = MerchandiseSalesCalculator.PerformanceFactor(strugglingRow, 18);
            double high = MerchandiseSalesCalculator.PerformanceFactor(leadingRow, 18);

            Assert.True(high > low);
        }

        [Fact]
        public void TryBuyStock_DeductsBalance_AndIncreasesStockAndExpense()
        {
            var team = Team(balance: 10_000);
            var inventory = new MerchandiseInventory
            {
                Articles = [new MerchandiseArticleStock { Type = MerchandiseArticleType.Poster, Stock = 0, SellPrice = 5 }],
            };

            bool applied = MerchandiseService.TryBuyStock(team, inventory, MerchandiseArticleType.Poster, 100);

            Assert.True(applied);
            Assert.Equal(100, inventory.Articles[0].Stock);
            Assert.Equal(10_000 - 100 * MerchandiseCatalog.Get(MerchandiseArticleType.Poster).WholesaleCost, team.Finances!.CurrentBalance);
            Assert.Equal(100 * MerchandiseCatalog.Get(MerchandiseArticleType.Poster).WholesaleCost, team.Finances.MerchandiseArticleExpense);
        }

        [Fact]
        public void TryBuyStock_InsufficientFunds_ReturnsFalse_AndDoesNotChangeStock()
        {
            var team = Team(balance: 10);
            var inventory = new MerchandiseInventory
            {
                Articles = [new MerchandiseArticleStock { Type = MerchandiseArticleType.FanTrikot, Stock = 0, SellPrice = 45 }],
            };

            bool applied = MerchandiseService.TryBuyStock(team, inventory, MerchandiseArticleType.FanTrikot, 5);

            Assert.False(applied);
            Assert.Equal(0, inventory.Articles[0].Stock);
            Assert.Equal(10, team.Finances!.CurrentBalance);
        }

        [Fact]
        public void SetPrice_NeverGoesBelowWholesaleCost()
        {
            var inventory = new MerchandiseInventory
            {
                Articles = [new MerchandiseArticleStock { Type = MerchandiseArticleType.Tasse, Stock = 0, SellPrice = 9 }],
            };

            MerchandiseService.SetPrice(inventory, MerchandiseArticleType.Tasse, 1);

            Assert.Equal(MerchandiseCatalog.Get(MerchandiseArticleType.Tasse).WholesaleCost, inventory.Articles[0].SellPrice);
        }

        [Fact]
        public void GetCampaignCost_StrongerLeagueTierCostsMoreThanWeakerTier()
        {
            // Tier 1 = top league (highest number = weakest per LeagueTier convention).
            // Same membership fee for both, so only the tier baseline differs.
            int tier1Cost = ClubMembershipService.GetCampaignCost(1, membershipFeePerMember: 100);
            int tier4Cost = ClubMembershipService.GetCampaignCost(4, membershipFeePerMember: 100);

            Assert.True(tier1Cost > tier4Cost);
        }

        [Fact]
        public void GetCampaignCost_NeverBelowFifteenThousand()
        {
            for (int tier = 1; tier <= 4; tier++)
                Assert.True(ClubMembershipService.GetCampaignCost(tier, membershipFeePerMember: 1) >= 15_000);
        }

        [Fact]
        public void GetCampaignCost_HigherMembershipFee_CostsMore()
        {
            int cheapFeeCost = ClubMembershipService.GetCampaignCost(2, membershipFeePerMember: 60);
            int expensiveFeeCost = ClubMembershipService.GetCampaignCost(2, membershipFeePerMember: 200);

            Assert.True(expensiveFeeCost > cheapFeeCost);
        }

        [Fact]
        public void TryLaunchCampaign_DeductsCostImmediately_ButDoesNotAddMembersYet()
        {
            var team = Team(tier: 3, balance: 100_000, clubMembers: 10_000, withDirectorOfFootball: true);
            team.Finances!.MembershipFeePerMember = 90;
            int cost = ClubMembershipService.GetCampaignCost(3, team.Finances.MembershipFeePerMember);
            var start = new DateTime(2026, 1, 1);

            bool applied = ClubMembershipService.TryLaunchCampaign(team, start, performanceFactor: 1.0, new Random(1));

            Assert.True(applied);
            Assert.Equal(100_000 - cost, team.Finances!.CurrentBalance);
            // Members trickle in via ApplyCampaignDrip, not instantly on launch.
            Assert.Equal(10_000, team.Finances.ClubMembers);
            Assert.True(team.Finances.MembershipCampaignTotalGain > 0);
        }

        [Fact]
        public void TryLaunchCampaign_InsufficientFunds_ReturnsFalse()
        {
            var team = Team(tier: 1, balance: 0, withDirectorOfFootball: true);

            bool applied = ClubMembershipService.TryLaunchCampaign(team, DateTime.Today, performanceFactor: 1.0, new Random(1));

            Assert.False(applied);
        }

        [Fact]
        public void TryLaunchCampaign_WithoutDirectorOfFootball_ReturnsFalse()
        {
            var team = Team(tier: 4, balance: 1_000_000, withDirectorOfFootball: false);

            bool applied = ClubMembershipService.TryLaunchCampaign(team, DateTime.Today, performanceFactor: 1.0, new Random(1));

            Assert.False(applied);
        }

        [Fact]
        public void TryLaunchCampaign_WhileAlreadyActive_ReturnsFalse()
        {
            var team = Team(tier: 4, balance: 1_000_000, withDirectorOfFootball: true);
            var start = new DateTime(2026, 1, 1);
            Assert.True(ClubMembershipService.TryLaunchCampaign(team, start, performanceFactor: 1.0, new Random(1)));

            bool secondAttempt = ClubMembershipService.TryLaunchCampaign(team, start.AddDays(5), performanceFactor: 1.0, new Random(1));

            Assert.False(secondAttempt);
        }

        [Fact]
        public void ApplyCampaignDrip_AddsMembersGraduallyOverTheCampaign_NotAllAtOnce()
        {
            var team = Team(tier: 4, balance: 1_000_000, clubMembers: 5_000, withDirectorOfFootball: true);
            var start = new DateTime(2026, 1, 1);
            ClubMembershipService.TryLaunchCampaign(team, start, performanceFactor: 1.0, new Random(1));
            int totalGain = team.Finances!.MembershipCampaignTotalGain;

            int afterOneDay = ClubMembershipService.ApplyCampaignDrip(team, start.AddDays(1));
            Assert.True(afterOneDay > 0 && afterOneDay < totalGain);

            int membersAfterFirstDrip = team.Finances.ClubMembers;
            Assert.True(membersAfterFirstDrip < 5_000 + totalGain);

            // Fast-forward to well past the campaign end - the rest trickles in, then it closes.
            ClubMembershipService.ApplyCampaignDrip(team, start.AddDays(200));

            Assert.Equal(5_000 + totalGain, team.Finances.ClubMembers);
            Assert.Null(team.Finances.MembershipCampaignEndDate);
            Assert.False(ClubMembershipService.IsCampaignActive(team.Finances, start.AddDays(200)));
        }

        [Fact]
        public void ApplyCampaignDrip_NeverExceedsTierHardCap()
        {
            var team = Team(tier: 4, balance: 100_000_000, clubMembers: 9_500, withDirectorOfFootball: true);
            var start = new DateTime(2026, 1, 1);
            // Repeatedly launch+drip campaigns to breeze straight past the tier-4 natural
            // range (3,000-10,000) - the hard cap (15,000 for tier 4) must still hold.
            for (int i = 0; i < 5; i++)
            {
                var launch = start.AddDays(i * 80);
                Assert.True(ClubMembershipService.TryLaunchCampaign(team, launch, performanceFactor: 1.8, new Random(i)));
                ClubMembershipService.ApplyCampaignDrip(team, launch.AddDays(80));
            }

            Assert.True(team.Finances!.ClubMembers <= 15_000);
        }

        [Fact]
        public void TryRunAiCampaignTick_WithoutDirectorOfFootball_NeverLaunches()
        {
            var team = Team(tier: 4, balance: 1_000_000, withDirectorOfFootball: false);
            var random = new Random(1);

            for (int i = 0; i < 50; i++)
            {
                bool launched = ClubMembershipService.TryRunAiCampaignTick(
                    team, DateTime.Today, performanceFactor: 1.8, Difficulty.Hard, cautionFactor: 1.0, random);
                Assert.False(launched);
            }
        }

        [Fact]
        public void TryRunAiCampaignTick_LowCautionFactor_NeverLaunches()
        {
            var team = Team(tier: 4, balance: 1_000_000, withDirectorOfFootball: true);
            var random = new Random(1);

            for (int i = 0; i < 50; i++)
            {
                bool launched = ClubMembershipService.TryRunAiCampaignTick(
                    team, DateTime.Today.AddDays(i * 80), performanceFactor: 1.8, Difficulty.Hard, cautionFactor: 0.1, random);
                Assert.False(launched);
            }
        }

        [Fact]
        public void TryRunAiCampaignTick_HardDifficulty_LaunchesMoreOftenThanEasy_OverManyWeeks()
        {
            int hardLaunches = CountAiLaunchesOverWeeks(Difficulty.Hard, weeks: 200, seed: 1);
            int easyLaunches = CountAiLaunchesOverWeeks(Difficulty.Easy, weeks: 200, seed: 1);

            Assert.True(hardLaunches > easyLaunches);
        }

        private static int CountAiLaunchesOverWeeks(Difficulty difficulty, int weeks, int seed)
        {
            var team = Team(tier: 4, balance: 100_000_000, withDirectorOfFootball: true);
            var random = new Random(seed);
            var date = new DateTime(2026, 1, 1);
            int launches = 0;

            for (int week = 0; week < weeks; week++)
            {
                date = date.AddDays(7);
                if (ClubMembershipService.TryRunAiCampaignTick(team, date, performanceFactor: 1.0, difficulty, cautionFactor: 1.0, random))
                    launches++;
            }

            return launches;
        }

        [Fact]
        public void ApplyCampaignDrip_SameDateTwice_DoesNotDoubleApply()
        {
            var team = Team(tier: 4, balance: 1_000_000, clubMembers: 1_000, withDirectorOfFootball: true);
            var start = new DateTime(2026, 1, 1);
            ClubMembershipService.TryLaunchCampaign(team, start, performanceFactor: 1.0, new Random(1));

            ClubMembershipService.ApplyCampaignDrip(team, start.AddDays(10));
            int membersAfterFirstCall = team.Finances!.ClubMembers;
            ClubMembershipService.ApplyCampaignDrip(team, start.AddDays(10));

            Assert.Equal(membersAfterFirstCall, team.Finances.ClubMembers);
        }

        [Fact]
        public void MerchandiseAdvisor_TopFinancialManagement_GivesExactRecommendation()
        {
            var article = new MerchandiseArticleStock
            {
                Type = MerchandiseArticleType.FanTrikot, Stock = 0,
                SellPrice = MerchandiseCatalog.Get(MerchandiseArticleType.FanTrikot).DefaultSellPrice,
            };

            var rec = MerchandiseAdvisor.Recommend(article, 50_000, performanceFactor: 1.0, DateTime.Today, bestPlayerRating: null, financialManagement: 95);

            Assert.True(rec.IsExact);
            Assert.Equal(rec.RecommendedMin, rec.RecommendedMax);
            Assert.True(rec.RecommendedMin > 0);
        }

        [Fact]
        public void MerchandiseAdvisor_LowFinancialManagement_GivesWideBand()
        {
            var article = new MerchandiseArticleStock
            {
                Type = MerchandiseArticleType.FanTrikot, Stock = 0,
                SellPrice = MerchandiseCatalog.Get(MerchandiseArticleType.FanTrikot).DefaultSellPrice,
            };

            var rec = MerchandiseAdvisor.Recommend(article, 50_000, performanceFactor: 1.0, DateTime.Today, bestPlayerRating: null, financialManagement: 10);

            Assert.False(rec.IsExact);
            Assert.True(rec.RecommendedMax > rec.RecommendedMin);
        }

        [Fact]
        public void MerchandiseAdvisor_AlreadyOverstocked_RecommendsZero()
        {
            var article = new MerchandiseArticleStock
            {
                Type = MerchandiseArticleType.FanTrikot, Stock = 100_000,
                SellPrice = MerchandiseCatalog.Get(MerchandiseArticleType.FanTrikot).DefaultSellPrice,
            };

            var rec = MerchandiseAdvisor.Recommend(article, 5_000, performanceFactor: 1.0, DateTime.Today, bestPlayerRating: null, financialManagement: 95);

            Assert.Equal(0, rec.RecommendedMin);
            Assert.Equal(0, rec.RecommendedMax);
        }

        [Fact]
        public void TrySellBackStock_RefundsHalfWholesaleCost_AndReducesStockAndExpense()
        {
            var team = Team(balance: 0);
            team.Finances!.MerchandiseArticleExpense = 10_000;
            var inventory = new MerchandiseInventory
            {
                Articles = [new MerchandiseArticleStock { Type = MerchandiseArticleType.FanTrikot, Stock = 100, SellPrice = 75 }],
            };
            int wholesale = MerchandiseCatalog.Get(MerchandiseArticleType.FanTrikot).WholesaleCost;

            bool applied = MerchandiseService.TrySellBackStock(team, inventory, MerchandiseArticleType.FanTrikot, 50);

            Assert.True(applied);
            Assert.Equal(50, inventory.Articles[0].Stock);
            int expectedRefund = (int)Math.Round(50 * wholesale * MerchandiseService.SellBackRefundFraction);
            Assert.Equal(expectedRefund, team.Finances.CurrentBalance);
            Assert.Equal(10_000 - expectedRefund, team.Finances.MerchandiseArticleExpense);
        }

        [Fact]
        public void TrySellBackStock_MoreThanAvailable_ReturnsFalse()
        {
            var team = Team();
            var inventory = new MerchandiseInventory
            {
                Articles = [new MerchandiseArticleStock { Type = MerchandiseArticleType.FanTrikot, Stock = 5, SellPrice = 75 }],
            };

            bool applied = MerchandiseService.TrySellBackStock(team, inventory, MerchandiseArticleType.FanTrikot, 10);

            Assert.False(applied);
            Assert.Equal(5, inventory.Articles[0].Stock);
        }
    }
}
