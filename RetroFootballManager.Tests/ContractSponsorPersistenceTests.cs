using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    // Round-trip persistence for the new M4 economy entities (Contract/Sponsor/Sponsorship),
    // following the same save-then-reload pattern as SaveTeamProgressTests.
    public class ContractSponsorPersistenceTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private TeamRepository _teams = null!;
        private ContractRepository _contracts = null!;
        private SponsorRepository _sponsors = null!;
        private SponsorshipRepository _sponsorships = null!;

        public ContractSponsorPersistenceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_economy_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _teams = new TeamRepository(_db);
            _contracts = new ContractRepository(_db);
            _sponsors = new SponsorRepository(_db);
            _sponsorships = new SponsorshipRepository(_db);
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public async Task Contract_SavedAndReloaded_RoundTripsForEmployeeHolder()
        {
            var team = TestHelpers.CreateTeam("Vertrags FC", baseRating: 60);
            await _teams.SaveTeamAsync(team);

            var contract = new Contract
            {
                HolderId = 42,
                HolderType = ContractHolderType.Employee,
                TeamId = team.Id,
                StartDate = new DateTime(2026, 8, 1),
                EndDate = new DateTime(2029, 8, 1),
                AnnualSalary = 60_000,
                MarketValue = 250_000,
                SigningBonus = 5_000,
                ReleaseClause = 1_000_000,
            };
            await _contracts.SaveAsync(contract);

            var loaded = await _contracts.GetByTeamAsync(team.Id);
            Assert.Single(loaded);
            Assert.Equal(42, loaded[0].HolderId);
            Assert.Equal(ContractHolderType.Employee, loaded[0].HolderType);
            Assert.Equal(60_000, loaded[0].AnnualSalary);

            var byHolder = await _contracts.GetByHolderAsync(42, ContractHolderType.Employee);
            Assert.Single(byHolder);
        }

        [Fact]
        public async Task Sponsor_CatalogEntry_SavedAndReloaded()
        {
            var sponsor = new Sponsor
            {
                Name = "Testbank AG",
                SponsorType = SponsorType.Main,
                MinTier = 2,
                SeasonPayment = 500_000,
                BonusPerWin = 10_000,
                BonusPerPromotion = 100_000,
            };
            await _sponsors.SaveAsync(sponsor);

            var loaded = await _sponsors.GetAllAsync();
            Assert.Single(loaded);
            Assert.Equal("Testbank AG", loaded[0].Name);
            Assert.Equal(SponsorType.Main, loaded[0].SponsorType);
        }

        [Fact]
        public async Task Sponsorship_SavedAndReloaded_ScopedToTeam()
        {
            var teamA = TestHelpers.CreateTeam("Sponsor FC A", baseRating: 55);
            var teamB = TestHelpers.CreateTeam("Sponsor FC B", baseRating: 55);
            await _teams.SaveTeamAsync(teamA);
            await _teams.SaveTeamAsync(teamB);

            await _sponsorships.SaveAsync(new Sponsorship
            {
                TeamId = teamA.Id,
                SponsorId = 1,
                SponsorType = SponsorType.Perimeter,
                StartSeason = 1,
                Duration = 2,
            });

            var forA = await _sponsorships.GetByTeamAsync(teamA.Id);
            var forB = await _sponsorships.GetByTeamAsync(teamB.Id);
            Assert.Single(forA);
            Assert.Empty(forB);
        }

        [Fact]
        public async Task DeleteTeamAsync_AlsoRemovesContractsAndSponsorships()
        {
            var team = TestHelpers.CreateTeam("Loesch FC", baseRating: 55);
            await _teams.SaveTeamAsync(team);

            await _contracts.SaveAsync(new Contract
            {
                HolderId = 1,
                HolderType = ContractHolderType.Employee,
                TeamId = team.Id,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(2),
            });
            await _sponsorships.SaveAsync(new Sponsorship
            {
                TeamId = team.Id,
                SponsorId = 1,
                SponsorType = SponsorType.Main,
                StartSeason = 1,
                Duration = 2,
            });

            await _teams.DeleteTeamAsync(team.Id);

            Assert.Empty(await _contracts.GetByTeamAsync(team.Id));
            Assert.Empty(await _sponsorships.GetByTeamAsync(team.Id));
        }
    }
}
