using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class StaffMarketServiceTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private TeamRepository _teamRepo = null!;
        private ContractRepository _contractRepo = null!;
        private StaffMarketService _service = null!;

        public StaffMarketServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_staffmarket_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _teamRepo = new TeamRepository(_db);
            _contractRepo = new ContractRepository(_db);
            _service = new StaffMarketService(_db, _teamRepo, _contractRepo, new Random(42));
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public void GenerateCandidates_ProducesRequestedCount_WithVariedTypes()
        {
            var candidates = _service.GenerateCandidates(teamTier: 2, count: 10);

            Assert.Equal(10, candidates.Count);
            Assert.True(candidates.Select(c => c.EmployeeType).Distinct().Count() > 1);
        }

        [Fact]
        public async Task HireAsync_AddsEmployeeAndCreatesContract_WithCorrectEndDate()
        {
            var team = TestHelpers.CreateTeam("Personal FC", baseRating: 55);
            await _teamRepo.SaveTeamAsync(team);

            var candidate = _service.GenerateCandidates(teamTier: 2, count: 1)[0];
            var hireDate = new DateTime(2026, 8, 1);
            var contract = await _service.HireAsync(team, candidate, hireDate, durationSeasons: 3);

            Assert.Contains(team.Employees, e => e.Id == candidate.Id);
            Assert.NotEqual(0, candidate.Id);
            Assert.Equal(candidate.Id, contract.HolderId);
            Assert.Equal(ContractHolderType.Employee, contract.HolderType);
            Assert.Equal(new DateTime(2029, 8, 1), contract.EndDate);

            var loaded = await _teamRepo.GetTeamAsync(team.Id);
            Assert.Contains(loaded!.Employees, e => e.Id == candidate.Id);
        }

        [Fact]
        public async Task FireAsync_RemovesEmployeeAndContract()
        {
            var team = TestHelpers.CreateTeam("Feuer FC", baseRating: 55);
            await _teamRepo.SaveTeamAsync(team);

            var candidate = _service.GenerateCandidates(teamTier: 2, count: 1)[0];
            await _service.HireAsync(team, candidate, new DateTime(2026, 8, 1));

            await _service.FireAsync(team, candidate);

            Assert.DoesNotContain(team.Employees, e => e.Id == candidate.Id);
            Assert.Empty(await _contractRepo.GetByHolderAsync(candidate.Id, ContractHolderType.Employee));
        }
    }
}
