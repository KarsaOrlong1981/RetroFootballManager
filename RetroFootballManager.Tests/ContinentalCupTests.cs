using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class ContinentalCupTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private SaveGameService _saveGame = null!;
        private CupTieRepository _cupTieRepo = null!;
        private TeamRepository _teamRepo = null!;
        private CupMatchDayService _cupMatchDayService = null!;

        public ContinentalCupTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_continental_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            _saveGame = new SaveGameService(_db);
            await _saveGame.InitializeAsync();
            _cupTieRepo = new CupTieRepository(_db);
            _teamRepo = new TeamRepository(_db);
            _cupMatchDayService = new CupMatchDayService(_cupTieRepo, _teamRepo, new Random(1));
        }

        public async Task DisposeAsync()
        {
            await _saveGame.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public async Task Season1_SeedsChampionsLeagueAndEuropaCup_WithFirstLiga1TeamsAsQualifiers()
        {
            var (leagues, teams) = UniverseGenerator.CreateUniverse(season: 1, random: new Random(2));
            var manager = teams[0];
            var seasonStart = new DateTime(2026, 8, 1);

            await _saveGame.StartNewCareerAsync("Test", 1, leagues, teams, manager, seasonStart);

            var clTies = await _cupTieRepo.GetBySeasonAsync(1, CompetitionType.ChampionsLeague);
            var elTies = await _cupTieRepo.GetBySeasonAsync(1, CompetitionType.EuropaCup);

            Assert.Equal(96, clTies.Count); // 8 Gruppen x 12 Partien
            Assert.Equal(96, elTies.Count);

            var tier1Ordered = teams.Where(t => t.LeagueTier == 1).OrderBy(t => t.Id).ToList();
            var expectedClIds = tier1Ordered.Take(4).Select(t => t.Id).ToHashSet();
            var expectedElIds = tier1Ordered.Skip(4).Take(3).Select(t => t.Id).ToHashSet();

            var clTeamIds = clTies.SelectMany(t => new[] { t.HomeTeamId, t.AwayTeamId }).ToHashSet();
            var elTeamIds = elTies.SelectMany(t => new[] { t.HomeTeamId, t.AwayTeamId }).ToHashSet();

            Assert.True(expectedClIds.All(id => clTeamIds.Contains(id)));
            Assert.True(expectedElIds.All(id => elTeamIds.Contains(id)));
        }

        [Fact]
        public async Task ChampionsLeagueDates_AreAlwaysWednesdays_EuropaCupAlwaysThursdays()
        {
            var (leagues, teams) = UniverseGenerator.CreateUniverse(season: 1, random: new Random(3));
            var manager = teams[0];
            await _saveGame.StartNewCareerAsync("Test", 1, leagues, teams, manager, new DateTime(2026, 8, 1));

            var clTies = await _cupTieRepo.GetBySeasonAsync(1, CompetitionType.ChampionsLeague);
            var elTies = await _cupTieRepo.GetBySeasonAsync(1, CompetitionType.EuropaCup);

            Assert.All(clTies, t => Assert.Equal(DayOfWeek.Wednesday, t.Date.DayOfWeek));
            Assert.All(elTies, t => Assert.Equal(DayOfWeek.Thursday, t.Date.DayOfWeek));
        }

        [Fact]
        public async Task GetNextCompetitionTieForTeamAsync_FindsQualifiedTeamsGroupMatch()
        {
            var (leagues, teams) = UniverseGenerator.CreateUniverse(season: 1, random: new Random(4));
            var seasonStart = new DateTime(2026, 8, 1);
            var tier1Ordered = teams.Where(t => t.LeagueTier == 1).OrderBy(t => t.Id).ToList();
            var qualifier = tier1Ordered[0];

            await _saveGame.StartNewCareerAsync("Test", 1, leagues, teams, qualifier, seasonStart);
            var allTeams = await _saveGame.GetAllTeamsAsync(); // inkl. erfundener CL-Vereine

            var tie = await _saveGame.GetNextCompetitionTieForTeamAsync(
                CompetitionType.ChampionsLeague, season: 1, qualifier.Id, allTeams, _cupMatchDayService, seasonStart);

            Assert.NotNull(tie);
            Assert.True(tie!.HomeTeamId == qualifier.Id || tie.AwayTeamId == qualifier.Id);
            Assert.Equal(0, tie.Round);
        }

        [Fact]
        public async Task GetNextCompetitionTieForTeamAsync_NonQualifiedTeam_ReturnsNull()
        {
            var (leagues, teams) = UniverseGenerator.CreateUniverse(season: 1, random: new Random(5));
            var seasonStart = new DateTime(2026, 8, 1);
            var manager = teams.First(t => t.LeagueTier == 4);

            await _saveGame.StartNewCareerAsync("Test", 1, leagues, teams, manager, seasonStart);
            var allTeams = await _saveGame.GetAllTeamsAsync();

            var tie = await _saveGame.GetNextCompetitionTieForTeamAsync(
                CompetitionType.ChampionsLeague, season: 1, manager.Id, allTeams, _cupMatchDayService, seasonStart);

            Assert.Null(tie);
        }

        [Fact]
        public async Task FullSeasonProgression_ChampionsLeague_ReachesFinalAsSingleNeutralMatch()
        {
            var (leagues, teams) = UniverseGenerator.CreateUniverse(season: 1, random: new Random(7));
            var seasonStart = new DateTime(2026, 8, 1);
            var manager = teams.First(t => t.LeagueTier == 4); // nicht qualifiziert - simuliert durch

            await _saveGame.StartNewCareerAsync("Test", 1, leagues, teams, manager, seasonStart);
            var allTeams = await _saveGame.GetAllTeamsAsync();

            // Treibt den gesamten Wettbewerb durch, indem wiederholt nach der nächsten Partie eines
            // NICHT teilnehmenden Teams gefragt wird - GetNextCompetitionTieForTeamAsync simuliert
            // dabei jede Runde/jeden Spieltag automatisch bis der Wettbewerb vorbei ist (null).
            CupTie? tie;
            int safety = 0;
            do
            {
                tie = await _saveGame.GetNextCompetitionTieForTeamAsync(
                    CompetitionType.ChampionsLeague, season: 1, manager.Id, allTeams, _cupMatchDayService, seasonStart);
                safety++;
            } while (tie is not null && safety < 500);

            Assert.Null(tie);

            var allTies = await _cupTieRepo.GetBySeasonAsync(1, CompetitionType.ChampionsLeague);
            // 96 Gruppenspiele + 16 (Achtelfinale) + 8 (Viertelfinale) + 4 (Halbfinale) + 1 (Finale)
            Assert.Equal(125, allTies.Count);
            Assert.True(allTies.All(t => t.Played));

            var final = allTies.Single(t => t.Round == CupDrawService.RoundFinal);
            Assert.Equal(CupTie.LegNone, final.LegNumber);

            foreach (var round in new[] { CupDrawService.RoundLastSixteen, CupDrawService.RoundQuarterFinal, CupDrawService.RoundSemiFinal })
            {
                var roundTies = allTies.Where(t => t.Round == round).ToList();
                Assert.All(roundTies, t => Assert.NotEqual(CupTie.LegNone, t.LegNumber));
            }
        }

        [Fact]
        public async Task IsTeamInCompetitionAsync_TrueForQualifier_FalseOtherwise()
        {
            var (leagues, teams) = UniverseGenerator.CreateUniverse(season: 1, random: new Random(6));
            var seasonStart = new DateTime(2026, 8, 1);
            var tier1Ordered = teams.Where(t => t.LeagueTier == 1).OrderBy(t => t.Id).ToList();
            var qualifier = tier1Ordered[0];
            var nonQualifier = teams.First(t => t.LeagueTier == 4);

            await _saveGame.StartNewCareerAsync("Test", 1, leagues, teams, qualifier, seasonStart);

            Assert.True(await _saveGame.IsTeamInCompetitionAsync(CompetitionType.ChampionsLeague, 1, qualifier.Id));
            Assert.False(await _saveGame.IsTeamInCompetitionAsync(CompetitionType.ChampionsLeague, 1, nonQualifier.Id));
        }
    }
}
