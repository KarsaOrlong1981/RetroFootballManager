using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class UniverseEconomyTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private SaveGameService _saveGame = null!;

        public UniverseEconomyTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_uni_econ_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            _saveGame = new SaveGameService(_db);
            await _saveGame.InitializeAsync();
        }

        public async Task DisposeAsync()
        {
            await _saveGame.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public void CreateUniverse_ProducesTeamsWithValidStadiumFields()
        {
            var (_, teams) = UniverseGenerator.CreateUniverse(season: 1, random: new Random(1));

            Assert.All(teams, team =>
            {
                Assert.NotNull(team.Stadium);
                Assert.True(team.Stadium!.SeatingCapacity > 0);
                Assert.True(team.Stadium.Capacity > 0);
                Assert.InRange(team.Stadium.ComfortLevel, 1, 5);
                Assert.True(team.Stadium.SeatPrice > 0);
            });
        }

        [Fact]
        public async Task StartNewCareerAsync_SeedsAtLeastOneSponsorshipPerTeam_AndCoachContract()
        {
            var (leagues, teams) = UniverseGenerator.CreateUniverse(season: 1, random: new Random(3));
            var manager = teams[0];

            await _saveGame.StartNewCareerAsync("Test", 1, leagues, teams, manager, new DateTime(2026, 8, 1));

            var sponsorshipRepo = new SponsorshipRepository(_db);
            var contractRepo = new ContractRepository(_db);

            // Das eigene Team bekommt bewusst KEINEN automatischen Sponsor (frei wählbar über
            // die SponsorsPage), aber trotzdem den Start-Co-Trainer-Vertrag wie jedes andere Team.
            var managerSponsorships = await sponsorshipRepo.GetByTeamAsync(manager.Id);
            Assert.Empty(managerSponsorships);

            foreach (var team in teams.Where(t => t.Id != manager.Id).Take(5))
            {
                var sponsorships = await sponsorshipRepo.GetByTeamAsync(team.Id);
                Assert.NotEmpty(sponsorships);
            }

            foreach (var team in teams.Take(5))
            {
                var coach = team.Employees.First(e => e.EmployeeType == EmployeeType.AssistantCoach);
                var contracts = await contractRepo.GetByHolderAsync(coach.Id, ContractHolderType.Employee);
                Assert.Single(contracts);
                Assert.Equal(new DateTime(2029, 8, 1), contracts[0].EndDate);
            }
        }
    }
}
