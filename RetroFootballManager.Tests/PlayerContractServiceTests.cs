using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class PlayerContractServiceTests
    {
        [Fact]
        public void SeedInitialContracts_OnePerSeniorPlayer_TwoYearTerm()
        {
            var team = TestHelpers.CreateTeam("Test", baseRating: 60);
            team.Id = 7;
            var seasonStart = new DateTime(2026, 8, 1);

            var contracts = PlayerContractService.SeedInitialContracts(team, seasonStart);

            Assert.Equal(team.Players.Count, contracts.Count);
            Assert.All(contracts, c =>
            {
                Assert.Equal(ContractHolderType.Player, c.HolderType);
                Assert.Equal(team.Id, c.TeamId);
                Assert.Equal(seasonStart, c.StartDate);
                Assert.Equal(seasonStart.AddYears(2), c.EndDate);
            });
        }

        [Fact]
        public void SeedInitialContracts_SalaryScalesWithRating_AndStaysAffordableForWeakSquads()
        {
            var strong = TestHelpers.CreateTeam("Stark", baseRating: 90);
            var weak = TestHelpers.CreateTeam("Schwach", baseRating: 40);
            var seasonStart = new DateTime(2026, 8, 1);

            var strongContracts = PlayerContractService.SeedInitialContracts(strong, seasonStart);
            var weakContracts = PlayerContractService.SeedInitialContracts(weak, seasonStart);

            Assert.True(strongContracts[0].AnnualSalary > weakContracts[0].AnnualSalary);

            // Regressionsschutz für den historischen Budget-Crash-Bug: ein kompletter
            // Liga-4-Kader (schwache Spieler) darf nicht mehr Gehalt kosten als ein Liga-4-Team
            // realistisch im Startbudget hat (800.000 lt. UniverseGenerator.TierBalance).
            double weakSquadTotalWages = weakContracts.Sum(c => c.AnnualSalary);
            Assert.True(weakSquadTotalWages < 5_000_000);
        }

        [Fact]
        public void SeedInitialContracts_IgnoresYouthPlayers()
        {
            var team = TestHelpers.CreateTeam("Test", baseRating: 60);
            team.YouthPlayers.Add(new Player { Id = 999, Name = "Jugend", Rating = 50, IsYouthProspect = true });

            var contracts = PlayerContractService.SeedInitialContracts(team, seasonStart: new DateTime(2026, 8, 1));

            Assert.DoesNotContain(contracts, c => c.HolderId == 999);
        }

        [Fact]
        public void GetActiveContract_ExpiredContractsAreExcluded()
        {
            var contracts = new List<Contract>
            {
                new() { HolderId = 1, HolderType = ContractHolderType.Player, EndDate = new DateTime(2025, 8, 1) },
                new() { HolderId = 1, HolderType = ContractHolderType.Player, EndDate = new DateTime(2028, 8, 1) },
            };

            var active = PlayerContractService.GetActiveContract(1, contracts, asOf: new DateTime(2026, 8, 1));

            Assert.NotNull(active);
            Assert.Equal(new DateTime(2028, 8, 1), active!.EndDate);
        }

        [Fact]
        public void RenewContract_UpdatesSalaryAndExtendsEndDate()
        {
            var contract = new Contract { AnnualSalary = 100_000, EndDate = new DateTime(2028, 8, 1) };

            PlayerContractService.RenewContract(contract, newAnnualSalary: 150_000, additionalYears: 2);

            Assert.Equal(150_000, contract.AnnualSalary);
            Assert.Equal(new DateTime(2030, 8, 1), contract.EndDate);
        }
    }
}
