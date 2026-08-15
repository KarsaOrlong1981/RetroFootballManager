using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    // Covers the "attach the human's created ManagerProfile before StartNewCareerAsync"
    // step TeamSelectionViewModel.Start() performs - not the MAUI UI itself (ManagerCreation-
    // ViewModel), which needs a live app host to test meaningfully.
    public class ManagerCreationFlowTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private SaveGameService _service = null!;

        public ManagerCreationFlowTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_managercreation_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _service = new SaveGameService(new AppDatabase(_dbPath));
            await _service.InitializeAsync();
        }

        public async Task DisposeAsync()
        {
            await _service.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public async Task StartNewCareer_HumanManagerProfileAttachedBeforeStart_PersistsAndReloads()
        {
            var teams = new List<Team>();
            for (int i = 0; i < 2; i++)
            {
                var t = TestHelpers.CreateTeam($"T{i}", baseRating: 60);
                t.LeagueTier = 1;
                teams.Add(t);
            }

            var leagues = new List<League> { new() { Name = "Liga 1", Tier = 1, Season = 1 } };
            var managerTeam = teams[0];

            // AI-rolled profile (as UniverseGenerator would have set) - overwritten by the
            // human's own profile below, mirroring TeamSelectionViewModel.Start().
            managerTeam.ManagerProfile = ManagerProfileGenerator.Generate(1, Nationality.Germany, new Random(1), new DateTime(2026, 8, 1));

            var humanProfile = new ManagerProfile
            {
                IsHuman = true,
                FirstName = "Jörg",
                LastName = "Thomas",
                BirthDate = new DateTime(1985, 3, 1),
                License = CoachingLicense.Pro,
                TrainingDesign = 10,
                Motivation = 3,
                OffensiveCreation = 10,
                DefensiveOrganization = 3,
                InGameCoaching = 3,
                UnspentSkillPoints = 0,
            };
            managerTeam.ManagerProfile = humanProfile;

            var state = await _service.StartNewCareerAsync(
                "Karriere", season: 1, leagues, teams, managerTeam, seasonStart: new DateTime(2026, 8, 1));

            var loaded = await _service.LoadGameAsync();
            Assert.NotNull(loaded);
            var reloadedManagerTeam = loaded!.Value.Teams.First(t => t.Id == state.ManagerTeamId);

            Assert.NotNull(reloadedManagerTeam.ManagerProfile);
            Assert.True(reloadedManagerTeam.ManagerProfile!.IsHuman);
            Assert.Equal("Jörg", reloadedManagerTeam.ManagerProfile.FirstName);
            Assert.Equal("Thomas", reloadedManagerTeam.ManagerProfile.LastName);
            Assert.Equal(CoachingLicense.Pro, reloadedManagerTeam.ManagerProfile.License);
            Assert.Equal(10, reloadedManagerTeam.ManagerProfile.TrainingDesign);
            Assert.Equal(10, reloadedManagerTeam.ManagerProfile.OffensiveCreation);
        }

        [Fact]
        public async Task StartNewCareer_AiTeams_KeepTheirGeneratedManagerProfiles()
        {
            var teams = new List<Team>();
            for (int i = 0; i < 2; i++)
            {
                var t = TestHelpers.CreateTeam($"T{i}", baseRating: 60);
                t.LeagueTier = 1;
                t.ManagerProfile = ManagerProfileGenerator.Generate(1, Nationality.Germany, new Random(i + 1), new DateTime(2026, 8, 1));
                teams.Add(t);
            }

            var leagues = new List<League> { new() { Name = "Liga 1", Tier = 1, Season = 1 } };
            var managerTeam = teams[0];
            var humanProfile = new ManagerProfile { IsHuman = true, FirstName = "Human", LastName = "Manager", License = CoachingLicense.Pro };
            managerTeam.ManagerProfile = humanProfile;

            var state = await _service.StartNewCareerAsync(
                "Karriere", season: 1, leagues, teams, managerTeam, seasonStart: new DateTime(2026, 8, 1));

            var loaded = await _service.LoadGameAsync();
            var aiTeam = loaded!.Value.Teams.First(t => t.Id != state.ManagerTeamId);

            Assert.NotNull(aiTeam.ManagerProfile);
            Assert.False(aiTeam.ManagerProfile!.IsHuman);
        }
    }
}
