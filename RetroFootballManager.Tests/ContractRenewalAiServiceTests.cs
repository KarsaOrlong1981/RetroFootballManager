using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class ContractRenewalAiServiceTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private ContractRepository _contractRepo = null!;

        private static readonly DateTime Today = new(2026, 9, 1);

        public ContractRenewalAiServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_renewal_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _contractRepo = new ContractRepository(_db);
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        private static Contract ExpiringContract(int playerId, DateTime endDate) => new()
        {
            HolderId = playerId, HolderType = ContractHolderType.Player, EndDate = endDate,
            AnnualSalary = 100_000,
        };

        [Fact]
        public async Task RunWeeklyTickAsync_RenewsAboveAverageOrYoungPlayer()
        {
            var team = TestHelpers.CreateTeam("KI FC", baseRating: 60);
            var goodPlayer = team.Players[0];
            goodPlayer.Rating = 70; // deutlich über Kaderschnitt (60)
            goodPlayer.Age = 30;

            var contract = ExpiringContract(goodPlayer.Id, Today.AddDays(30));
            await _contractRepo.SaveAsync(contract);

            await ContractRenewalAiService.RunWeeklyTickAsync(team, [contract], _contractRepo, Today);

            Assert.True(contract.EndDate > Today.AddDays(30));
            Assert.True(contract.AnnualSalary > 100_000);
        }

        [Fact]
        public async Task RunWeeklyTickAsync_DoesNotRenew_WeakAndOldPlayer()
        {
            var team = TestHelpers.CreateTeam("KI FC", baseRating: 60);
            var weakPlayer = team.Players[0];
            weakPlayer.Rating = 40; // deutlich unter Kaderschnitt
            weakPlayer.Age = 33;

            var contract = ExpiringContract(weakPlayer.Id, Today.AddDays(30));
            await _contractRepo.SaveAsync(contract);
            var originalEndDate = contract.EndDate;

            await ContractRenewalAiService.RunWeeklyTickAsync(team, [contract], _contractRepo, Today);

            Assert.Equal(originalEndDate, contract.EndDate);
            Assert.Equal(100_000, contract.AnnualSalary);
        }

        [Fact]
        public async Task RunWeeklyTickAsync_IgnoresContractsFarFromExpiry()
        {
            var team = TestHelpers.CreateTeam("KI FC", baseRating: 60);
            var player = team.Players[0];
            player.Rating = 70;

            var contract = ExpiringContract(player.Id, Today.AddYears(2));
            await _contractRepo.SaveAsync(contract);
            var originalEndDate = contract.EndDate;

            await ContractRenewalAiService.RunWeeklyTickAsync(team, [contract], _contractRepo, Today);

            Assert.Equal(originalEndDate, contract.EndDate);
        }
    }
}
