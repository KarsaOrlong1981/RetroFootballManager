using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class PlayerStatsServiceTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private TeamRepository _teamRepo = null!;
        private PlayerRepository _playerRepo = null!;
        private PlayerStatsService _stats = null!;

        public PlayerStatsServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_stats_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _teamRepo = new TeamRepository(_db);
            _playerRepo = new PlayerRepository(_db);
            _stats = new PlayerStatsService(_playerRepo, _teamRepo);
        }

        public async Task DisposeAsync()
        {
            await _db.Connection.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        private async Task<Team> CreateAndSaveTeamAsync(string name, int leagueTier)
        {
            var team = TestHelpers.CreateTeam(name, baseRating: 60);
            team.LeagueTier = leagueTier;
            await _teamRepo.SaveTeamAsync(team);
            return team;
        }

        private Task SaveStatsAsync(int playerId, int season, int goals = 0, int assists = 0,
            int yellow = 0, int red = 0, int conceded = 0, int appearances = 1, CompetitionType? competition = null) =>
            _playerRepo.SavePlayerStatsAsync(new PlayerStats
            {
                PlayerId = playerId,
                Season = season,
                Goals = goals,
                Assists = assists,
                YellowCards = yellow,
                RedCards = red,
                GoalsConceded = conceded,
                Appearances = appearances,
                Competition = competition,
            });

        [Fact]
        public async Task GetTopAsync_TopScorers_RanksByGoalsDescending_WithinLeagueOnly()
        {
            var teamA = await CreateAndSaveTeamAsync("A", leagueTier: 1);
            var teamB = await CreateAndSaveTeamAsync("B", leagueTier: 2);

            var topScorer = teamA.Players[9]; // Forward
            var midScorer = teamA.Players[8]; // Midfielder
            var otherLeagueScorer = teamB.Players[9];

            await SaveStatsAsync(topScorer.Id, season: 1, goals: 10);
            await SaveStatsAsync(midScorer.Id, season: 1, goals: 4);
            await SaveStatsAsync(otherLeagueScorer.Id, season: 1, goals: 99); // different league - must not appear

            var top = await _stats.GetTopAsync(StatCategory.TopScorers, season: 1, leagueTier: 1, matchdaysPlayed: 10);

            Assert.Equal(2, top.Count);
            Assert.Equal(topScorer.Id, top[0].PlayerId);
            Assert.Equal(10, top[0].Value);
            Assert.Equal(midScorer.Id, top[1].PlayerId);
        }

        [Fact]
        public async Task GetTopAsync_ScorerPoints_SumsGoalsAndAssists()
        {
            var team = await CreateAndSaveTeamAsync("A", leagueTier: 1);
            var playmaker = team.Players[8];
            var poacher = team.Players[9];

            await SaveStatsAsync(playmaker.Id, season: 1, goals: 2, assists: 6); // 8 points
            await SaveStatsAsync(poacher.Id, season: 1, goals: 7, assists: 0);   // 7 points

            var top = await _stats.GetTopAsync(StatCategory.ScorerPoints, season: 1, leagueTier: 1, matchdaysPlayed: 10);

            Assert.Equal(playmaker.Id, top[0].PlayerId);
            Assert.Equal(8, top[0].Value);
            Assert.Equal(poacher.Id, top[1].PlayerId);
            Assert.Equal(7, top[1].Value);
        }

        [Fact]
        public async Task GetTopAsync_FewestConceded_ExcludesGoalkeepersBelowMinimumAppearances()
        {
            var team = await CreateAndSaveTeamAsync("A", leagueTier: 1);
            var starter = team.Players[0]; // Goalkeeper (see TestHelpers lineup)
            Assert.Equal(Position.Goalkeeper, starter.Position);

            // Second "keeper": reuse a bench-style id but override Position via a second team.
            var teamB = await CreateAndSaveTeamAsync("B", leagueTier: 1);
            var cameoKeeper = teamB.Players[0];
            Assert.Equal(Position.Goalkeeper, cameoKeeper.Position);

            await SaveStatsAsync(starter.Id, season: 1, conceded: 20, appearances: 30);
            await SaveStatsAsync(cameoKeeper.Id, season: 1, conceded: 0, appearances: 1); // one cameo, 0 conceded

            // 30 matchdays played this season -> minimum appearances = 15, cameoKeeper is filtered out
            // even though 0 conceded goals would otherwise "win".
            var top = await _stats.GetTopAsync(StatCategory.FewestConceded, season: 1, leagueTier: 1, matchdaysPlayed: 30);

            Assert.Single(top);
            Assert.Equal(starter.Id, top[0].PlayerId);
        }

        [Fact]
        public async Task GetCompetitionTopAsync_IsolatesCupStatsFromLeagueStats_ForSamePlayerAndSeason()
        {
            var team = await CreateAndSaveTeamAsync("A", leagueTier: 1);
            var player = team.Players[9];

            await SaveStatsAsync(player.Id, season: 1, goals: 5); // Liga-Statistik
            await SaveStatsAsync(player.Id, season: 1, goals: 2, competition: CompetitionType.ChampionsLeague);

            var cupTop = await _stats.GetCompetitionTopAsync(StatCategory.TopScorers, season: 1, CompetitionType.ChampionsLeague);

            Assert.Single(cupTop);
            Assert.Equal(2, cupTop[0].Value);
        }

        [Fact]
        public async Task GetCompetitionTopAsync_TopScorers_RanksByCupGoalsOnly()
        {
            var team = await CreateAndSaveTeamAsync("A", leagueTier: 1);
            var topScorer = team.Players[9];
            var midScorer = team.Players[8];

            await SaveStatsAsync(topScorer.Id, season: 1, goals: 1, competition: CompetitionType.ChampionsLeague);
            await SaveStatsAsync(midScorer.Id, season: 1, goals: 9, competition: CompetitionType.EuropaCup); // anderer Wettbewerb

            var top = await _stats.GetCompetitionTopAsync(StatCategory.TopScorers, season: 1, CompetitionType.ChampionsLeague);

            Assert.Single(top);
            Assert.Equal(topScorer.Id, top[0].PlayerId);
        }
    }
}
