using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class SponsorPromotionBonusTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private SaveGameService _saveGame = null!;
        private SponsorRepository _sponsorRepo = null!;
        private SponsorshipRepository _sponsorshipRepo = null!;

        public SponsorPromotionBonusTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_sponsor_bonus_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            _saveGame = new SaveGameService(_db);
            _sponsorRepo = new SponsorRepository(_db);
            _sponsorshipRepo = new SponsorshipRepository(_db);
            await _saveGame.InitializeAsync();
        }

        public async Task DisposeAsync()
        {
            await _saveGame.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public async Task PaySponsorPromotionBonusesAsync_CreditsBonusForPromotedTeamWithActiveDeal()
        {
            var team = TestHelpers.CreateTeam("Aufsteiger", baseRating: 60);
            team.Id = 1;
            team.Finances = new Finances { CurrentBalance = 100_000, SponsorIncome = 0 };

            var sponsor = new Sponsor { Name = "Test Sponsor", SponsorType = SponsorType.Main, MinTier = 4, BonusPerPromotion = 250_000 };
            await _sponsorRepo.SaveAsync(sponsor);
            await _sponsorshipRepo.SaveAsync(new Sponsorship
            {
                TeamId = team.Id, SponsorId = sponsor.Id, SponsorType = SponsorType.Main, StartSeason = 1, Duration = 2,
            });

            var result = new SeasonEndResult(
                Season: 1,
                Leagues: [new LeagueTableResult(Tier: 4, Table: [], PromotedTeamIds: [team.Id], RelegatedTeamIds: [])],
                ManagerFinalPosition: 1, ManagerTier: 4, PointsAwarded: 100, ManagerOutcome: "Aufstieg", ManagerPromoted: true);

            await _saveGame.PaySponsorPromotionBonusesAsync([team], result);

            Assert.Equal(350_000, team.Finances.CurrentBalance);
            Assert.Equal(250_000, team.Finances.SponsorIncome);
        }

        [Fact]
        public async Task PaySponsorPromotionBonusesAsync_DoesNothing_ForNonPromotedTeam()
        {
            var team = TestHelpers.CreateTeam("Bleibt", baseRating: 60);
            team.Id = 2;
            team.Finances = new Finances { CurrentBalance = 100_000 };

            var sponsor = new Sponsor { Name = "Test Sponsor", SponsorType = SponsorType.Main, MinTier = 4, BonusPerPromotion = 250_000 };
            await _sponsorRepo.SaveAsync(sponsor);
            await _sponsorshipRepo.SaveAsync(new Sponsorship
            {
                TeamId = team.Id, SponsorId = sponsor.Id, SponsorType = SponsorType.Main, StartSeason = 1, Duration = 2,
            });

            var result = new SeasonEndResult(
                Season: 1,
                Leagues: [new LeagueTableResult(Tier: 4, Table: [], PromotedTeamIds: [], RelegatedTeamIds: [])],
                ManagerFinalPosition: 5, ManagerTier: 4, PointsAwarded: 25, ManagerOutcome: "Klassenerhalt", ManagerPromoted: false);

            await _saveGame.PaySponsorPromotionBonusesAsync([team], result);

            Assert.Equal(100_000, team.Finances.CurrentBalance);
        }

        [Fact]
        public async Task PaySponsorPromotionBonusesAsync_DoesNothing_WithoutActiveSponsorship()
        {
            var team = TestHelpers.CreateTeam("KeinSponsor", baseRating: 60);
            team.Id = 3;
            team.Finances = new Finances { CurrentBalance = 100_000 };

            var result = new SeasonEndResult(
                Season: 1,
                Leagues: [new LeagueTableResult(Tier: 4, Table: [], PromotedTeamIds: [team.Id], RelegatedTeamIds: [])],
                ManagerFinalPosition: 1, ManagerTier: 4, PointsAwarded: 100, ManagerOutcome: "Aufstieg", ManagerPromoted: true);

            await _saveGame.PaySponsorPromotionBonusesAsync([team], result);

            Assert.Equal(100_000, team.Finances.CurrentBalance);
        }
    }
}
