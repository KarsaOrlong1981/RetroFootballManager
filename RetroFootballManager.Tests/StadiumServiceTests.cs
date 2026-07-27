using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class StadiumServiceTests
    {
        private static Team CreateTeamWithStadiumAndFinances(int balance, int infrastructureLevel = 1)
        {
            var team = TestHelpers.CreateTeam("Ausbau FC", baseRating: 60);
            team.Stadium = new Stadium { SeatingCapacity = 10_000, InfrastructureLevel = infrastructureLevel };
            team.Finances = new Finances { CurrentBalance = balance };
            return team;
        }

        [Fact]
        public void HigherInfrastructureLevel_MakesSeatingCheaper()
        {
            var lowInfra = new Stadium { InfrastructureLevel = 1 };
            var highInfra = new Stadium { InfrastructureLevel = 5 };

            var lowCost = StadiumService.GetSeatingUpgradeCost(lowInfra, 1000);
            var highCost = StadiumService.GetSeatingUpgradeCost(highInfra, 1000);

            Assert.True(highCost.Amount < lowCost.Amount);
        }

        [Fact]
        public void LevelUpgradeCost_GetsSteeperAtHigherLevels()
        {
            var stadium = new Stadium { ComfortLevel = 1 };
            var fromOne = StadiumService.GetLevelUpgradeCost(stadium, StadiumUpgradeKind.Comfort);

            stadium.ComfortLevel = 4;
            var fromFour = StadiumService.GetLevelUpgradeCost(stadium, StadiumUpgradeKind.Comfort);

            Assert.True(fromFour.Amount > fromOne.Amount);
        }

        [Fact]
        public void TryApplyUpgrade_Fails_WhenBalanceTooLow()
        {
            var team = CreateTeamWithStadiumAndFinances(balance: 100);
            bool applied = StadiumService.TryApplyUpgrade(team, s => s.SeatingCapacity += 500, cost: 10_000);

            Assert.False(applied);
            Assert.Equal(10_000, team.Stadium!.SeatingCapacity);
            Assert.Equal(100, team.Finances!.CurrentBalance);
        }

        [Fact]
        public void TryApplyUpgrade_Succeeds_AndDeductsExactCost()
        {
            var team = CreateTeamWithStadiumAndFinances(balance: 50_000);
            bool applied = StadiumService.TryApplyUpgrade(team, s => s.SeatingCapacity += 500, cost: 10_000);

            Assert.True(applied);
            Assert.Equal(10_500, team.Stadium!.SeatingCapacity);
            Assert.Equal(40_000, team.Finances!.CurrentBalance);
        }

        [Fact]
        public void ApplySeatingUpgrade_IncreasesCapacityAndMaintenanceCosts()
        {
            var stadium = new Stadium { SeatingCapacity = 10_000, MaintenanceCosts = 5_000 };
            StadiumService.ApplySeatingUpgrade(stadium, 1_000);

            Assert.Equal(11_000, stadium.SeatingCapacity);
            Assert.True(stadium.MaintenanceCosts > 5_000);
        }

        [Fact]
        public void ApplyRoof_IncreasesMaintenanceCosts()
        {
            var stadium = new Stadium { MaintenanceCosts = 5_000 };
            StadiumService.ApplyRoof(stadium);

            Assert.True(stadium.HasRoof);
            Assert.True(stadium.MaintenanceCosts > 5_000);
        }

        [Fact]
        public void ApplyLevelUpgrade_IncreasesLevelAndMaintenanceCosts()
        {
            var stadium = new Stadium { ComfortLevel = 1, MaintenanceCosts = 5_000 };
            StadiumService.ApplyLevelUpgrade(stadium, StadiumUpgradeKind.Comfort);

            Assert.Equal(2, stadium.ComfortLevel);
            Assert.True(stadium.MaintenanceCosts > 5_000);
        }

        [Fact]
        public void Capacity_ComputedFromThreeTiers_UpdatesAfterUpgrade()
        {
            var stadium = new Stadium { SeatingCapacity = 20_000, StandingCapacity = 5_000, LogeCapacity = 200 };
            Assert.Equal(25_200, stadium.Capacity);

            stadium.SeatingCapacity += 1_000;
            Assert.Equal(26_200, stadium.Capacity);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(9_999, 0)]
        [InlineData(10_000, 1)]
        [InlineData(14_999, 1)]
        [InlineData(15_000, 2)]
        [InlineData(24_999, 2)]
        [InlineData(25_000, 3)]
        [InlineData(44_999, 3)]
        [InlineData(45_000, 4)]
        [InlineData(59_999, 4)]
        [InlineData(60_000, 5)]
        [InlineData(79_999, 5)]
        [InlineData(80_000, 6)]
        [InlineData(200_000, 6)]
        public void GetEvolutionStage_ReturnsCorrectBracket(int capacity, int expectedStage)
        {
            Assert.Equal(expectedStage, StadiumService.GetEvolutionStage(capacity));
        }

        [Fact]
        public void GetSeatingUpgradeCost_ScalesWithEvolutionStage()
        {
            var minimal = new Stadium { SeatingCapacity = 5_000 };
            var ultimativ = new Stadium { SeatingCapacity = 90_000 };

            var minimalCost = StadiumService.GetSeatingUpgradeCost(minimal, 1_000);
            var ultimativCost = StadiumService.GetSeatingUpgradeCost(ultimativ, 1_000);

            Assert.True(ultimativCost.Amount > minimalCost.Amount);
        }

        [Fact]
        public void GetStandingUpgradeCost_And_GetLogeUpgradeCost_ScaleWithEvolutionStage()
        {
            var minimal = new Stadium { SeatingCapacity = 5_000 };
            var ultimativ = new Stadium { SeatingCapacity = 90_000 };

            Assert.True(
                StadiumService.GetStandingUpgradeCost(ultimativ, 500).Amount >
                StadiumService.GetStandingUpgradeCost(minimal, 500).Amount);
            Assert.True(
                StadiumService.GetLogeUpgradeCost(ultimativ, 50).Amount >
                StadiumService.GetLogeUpgradeCost(minimal, 50).Amount);
        }

        [Fact]
        public void FullEvolutionClimb_FromMinimalToUltimativ_CostsFarMoreThanTier4MaxLoan()
        {
            var stadium = new Stadium { SeatingCapacity = 5_000 };
            double totalCost = 0;
            const int step = 1_000;

            while (stadium.Capacity < 90_000)
            {
                totalCost += StadiumService.GetSeatingUpgradeCost(stadium, step).Amount;
                stadium.SeatingCapacity += step;
            }

            var tier4Team = TestHelpers.CreateTeam("Kreisliga FC", baseRating: 40);
            tier4Team.LeagueTier = 4;
            int maxLoan = ClubLoanService.GetMaxLoanAmount(tier4Team);

            Assert.True(totalCost > maxLoan * 10);
        }
    }
}
