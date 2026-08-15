using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class ClubManagementAiServiceTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private StaffMarketService _staffMarket = null!;

        private static readonly DateTime Today = new(2026, 9, 1);

        public ClubManagementAiServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_club_ai_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            var teamRepo = new TeamRepository(_db);
            var contractRepo = new ContractRepository(_db);
            _staffMarket = new StaffMarketService(_db, teamRepo, contractRepo, new Random(1));
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public void TryUpgradeStadium_WithEnoughMoney_AppliesAnUpgrade()
        {
            var team = TestHelpers.CreateTeam("KI FC", baseRating: 60);
            team.Stadium = new Stadium { Name = "Arena", ComfortLevel = 1, CateringLevel = 1, MerchandiseLevel = 1, InfrastructureLevel = 1 };
            team.Finances = new Finances { CurrentBalance = 10_000_000 };

            bool upgraded = ClubManagementAiService.TryUpgradeStadium(team, new Random(1));

            Assert.True(upgraded);
            int totalLevels = team.Stadium.ComfortLevel + team.Stadium.CateringLevel
                + team.Stadium.MerchandiseLevel + team.Stadium.InfrastructureLevel;
            Assert.Equal(5, totalLevels); // eine Stufe von 4x1 auf insgesamt 5 erhöht
        }

        [Fact]
        public void TryUpgradeStadium_WithoutMoney_DoesNothing()
        {
            var team = TestHelpers.CreateTeam("KI FC", baseRating: 60);
            team.Stadium = new Stadium { Name = "Arena", ComfortLevel = 1, CateringLevel = 1, MerchandiseLevel = 1, InfrastructureLevel = 1 };
            team.Finances = new Finances { CurrentBalance = 100 };

            bool upgraded = ClubManagementAiService.TryUpgradeStadium(team, new Random(1));

            Assert.False(upgraded);
            Assert.Equal(1, team.Stadium.ComfortLevel);
        }

        [Fact]
        public async Task TryHireMissingStaffAsync_FillsMissingCoreRole()
        {
            var team = TestHelpers.CreateTeam("KI FC", baseRating: 60);
            team.Finances = new Finances { CurrentBalance = 10_000_000 };
            team.LeagueTier = 2;

            // GenerateCandidates streut zufällig über 10 Rollen - mehrere Versuche, bis die
            // gesuchte Rolle (AssistantCoach) im Kandidatenpool auftaucht.
            Employee? hired = null;
            for (int seed = 1; seed <= 20 && hired is null; seed++)
                hired = await ClubManagementAiService.TryHireMissingStaffAsync(team, _staffMarket, Today, new Random(seed));

            Assert.NotNull(hired);
            Assert.Contains(team.Employees, e => e.Id == hired!.Id);
        }

        [Fact]
        public async Task TryHireMissingStaffAsync_ReturnsNull_WhenCoreRolesAlreadyFilled()
        {
            var team = TestHelpers.CreateTeam("KI FC", baseRating: 60);
            team.Finances = new Finances { CurrentBalance = 10_000_000 };
            team.LeagueTier = 2;
            team.Employees.Add(new Employee { EmployeeType = EmployeeType.AssistantCoach, Name = "A" });
            team.Employees.Add(new Employee { EmployeeType = EmployeeType.FitnessCoach, Name = "B" });
            team.Employees.Add(new Employee { EmployeeType = EmployeeType.GoalkeeperCoach, Name = "C" });

            var hired = await ClubManagementAiService.TryHireMissingStaffAsync(team, _staffMarket, Today, new Random(1));

            Assert.Null(hired);
        }

        [Fact]
        public async Task TryHireMissingStaffAsync_ReturnsNull_WithoutBudget()
        {
            var team = TestHelpers.CreateTeam("KI FC", baseRating: 60);
            team.Finances = new Finances { CurrentBalance = 1 };
            team.LeagueTier = 2;

            var hired = await ClubManagementAiService.TryHireMissingStaffAsync(team, _staffMarket, Today, new Random(1));

            Assert.Null(hired);
        }

        [Fact]
        public async Task TryHireMissingStaffAsync_ReturnsNull_WhenBalanceNegative()
        {
            var team = TestHelpers.CreateTeam("Miese KI FC", baseRating: 60);
            team.Finances = new Finances { CurrentBalance = -1 };
            team.LeagueTier = 2;

            var hired = await ClubManagementAiService.TryHireMissingStaffAsync(team, _staffMarket, Today, new Random(1));

            Assert.Null(hired);
        }
    }
}
