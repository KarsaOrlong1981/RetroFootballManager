using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class SponsorServiceTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private SponsorService _service = null!;
        private SponsorRepository _sponsorRepo = null!;
        private SponsorshipRepository _sponsorshipRepo = null!;

        public SponsorServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_sponsor_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _sponsorRepo = new SponsorRepository(_db);
            _sponsorshipRepo = new SponsorshipRepository(_db);
            _service = new SponsorService(_sponsorRepo, _sponsorshipRepo);

            await _sponsorRepo.SaveAsync(new Sponsor { Name = "Tier1 Bank", SponsorType = SponsorType.Main, MinTier = 1, SeasonPayment = 1_000_000 });
            await _sponsorRepo.SaveAsync(new Sponsor { Name = "Regional Bank", SponsorType = SponsorType.Main, MinTier = 4, SeasonPayment = 50_000 });
            await _sponsorRepo.SaveAsync(new Sponsor { Name = "Ausrüster GmbH", SponsorType = SponsorType.Kit, MinTier = 2, SeasonPayment = 200_000 });
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public async Task GetAvailableOffers_ExcludesSponsorsBelowTeamTier()
        {
            var tier4Team = TestHelpers.CreateTeam("Tier4 FC", baseRating: 40);
            tier4Team.LeagueTier = 4;

            var offers = await _service.GetAvailableOffersAsync(tier4Team, SponsorType.Main);

            Assert.Single(offers);
            Assert.Equal("Regional Bank", offers[0].Name);
        }

        [Fact]
        public async Task GetAvailableOffers_Tier1Team_SeesAllMainSponsors()
        {
            var tier1Team = TestHelpers.CreateTeam("Tier1 FC", baseRating: 80);
            tier1Team.LeagueTier = 1;

            var offers = await _service.GetAvailableOffersAsync(tier1Team, SponsorType.Main);

            Assert.Equal(2, offers.Count);
        }

        [Fact]
        public async Task KitSlot_ExcludedForTeamsBelowTier2()
        {
            var tier3Team = TestHelpers.CreateTeam("Tier3 FC", baseRating: 50);
            tier3Team.LeagueTier = 3;

            var offers = await _service.GetAvailableOffersAsync(tier3Team, SponsorType.Kit);

            Assert.Empty(offers);
        }

        [Fact]
        public async Task SignAsync_ReplacesExistingDealInSameSlot_OnceExpired()
        {
            var team = TestHelpers.CreateTeam("Deal FC", baseRating: 60);
            team.Id = 1;
            team.LeagueTier = 1;

            var mainSponsors = await _sponsorRepo.GetAllAsync();
            var tier1Main = mainSponsors.Single(s => s.Name == "Tier1 Bank");
            var regionalMain = mainSponsors.Single(s => s.Name == "Regional Bank");

            await _service.SignAsync(team, tier1Main, currentSeason: 1, durationSeasons: 2);
            // Season 3: the 2-season deal signed in season 1 has expired (1 + 2 = 3).
            await _service.SignAsync(team, regionalMain, currentSeason: 3);

            var deals = await _sponsorshipRepo.GetByTeamAsync(team.Id);
            Assert.Single(deals);
            Assert.Equal(regionalMain.Id, deals[0].SponsorId);
        }

        [Fact]
        public async Task SignAsync_BlockedWhileCurrentDealStillRunning()
        {
            var team = TestHelpers.CreateTeam("Gesperrt FC", baseRating: 60);
            team.Id = 1;
            team.LeagueTier = 1;

            var mainSponsors = await _sponsorRepo.GetAllAsync();
            var tier1Main = mainSponsors.Single(s => s.Name == "Tier1 Bank");
            var regionalMain = mainSponsors.Single(s => s.Name == "Regional Bank");

            await _service.SignAsync(team, tier1Main, currentSeason: 1, durationSeasons: 2);
            var blocked = await _service.SignAsync(team, regionalMain, currentSeason: 2);

            Assert.Null(blocked);
            var deals = await _sponsorshipRepo.GetByTeamAsync(team.Id);
            Assert.Single(deals);
            Assert.Equal(tier1Main.Id, deals[0].SponsorId);
        }
    }
}
