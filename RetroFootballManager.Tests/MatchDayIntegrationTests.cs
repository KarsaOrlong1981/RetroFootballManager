using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    // Drives the matchday/season loop through the real services (persistence included).
    public class MatchDayIntegrationTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private readonly string _careerPath;
        private AppDatabase _db = null!;
        private SaveGameService _saveGame = null!;
        private MatchDayService _matchDay = null!;
        private FixtureRepository _fixtureRepo = null!;
        private CareerService _career = null!;

        public MatchDayIntegrationTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_md_{Guid.NewGuid():N}.db3");
            _careerPath = Path.Combine(Path.GetTempPath(), $"rfm_md_{Guid.NewGuid():N}.json");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            _saveGame = new SaveGameService(_db);
            _fixtureRepo = new FixtureRepository(_db);
            _matchDay = new MatchDayService(_fixtureRepo, new TeamRepository(_db), new PlayerRepository(_db), random: new Random(1));
            _career = new CareerService(_careerPath);
            await _saveGame.InitializeAsync();
        }

        public async Task DisposeAsync()
        {
            await _saveGame.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
            if (File.Exists(_careerPath)) File.Delete(_careerPath);
        }

        // Realistic shape (4 tiers) but small leagues (6 teams) so top-3/bottom-3 stay disjoint
        // and simulations run fast. Smaller squads keep inserts cheap.
        private static (List<League> Leagues, List<Team> Teams) BuildUniverse()
        {
            var rng = new Random(7);
            var leagues = new List<League>();
            var teams = new List<Team>();

            for (int tier = 1; tier <= 4; tier++)
            {
                leagues.Add(new League { Name = $"Liga {tier}", Tier = tier, Season = 1 });
                for (int i = 0; i < 6; i++)
                {
                    var players = PlayerGenerator.GenerateSquad(Nationality.Germany, 60, squadSize: 16, random: rng);
                    var team = new Team
                    {
                        Name = $"T{tier}-{i}",
                        ShortName = $"T{tier}{i}",
                        LeagueTier = tier,
                        Statistics = new TeamStats { Season = 1 },
                    };
                    team.Players.AddRange(players);
                    teams.Add(team);
                }
            }
            return (leagues, teams);
        }

        private static MatchResult SimulateHumanFixture(List<Team> teams, Fixture fixture)
        {
            var home = teams.First(t => t.Id == fixture.HomeTeamId);
            var away = teams.First(t => t.Id == fixture.AwayTeamId);
            MatchDayService.PrepareForMatch(home);
            MatchDayService.PrepareForMatch(away);
            return new Match(home, away, new Random(fixture.Id)).Simulate();
        }

        [Fact]
        public async Task PlayingAMatchday_MarksAllFixturesPlayed_AndAdvancesDate()
        {
            var (leagues, teams) = BuildUniverse();
            var manager = teams.First(t => t.LeagueTier == 4);
            var state = await _saveGame.StartNewCareerAsync(
                "Test", 1, leagues, teams, manager, new DateTime(2026, 8, 1));

            var fixtures = await _saveGame.GetFixturesAsync(1);
            var humanFixture = fixtures.First(f => f.Matchday == 1 &&
                (f.HomeTeamId == manager.Id || f.AwayTeamId == manager.Id));

            var startDate = state.CurrentDate;
            var humanResult = SimulateHumanFixture(teams, humanFixture);
            await _matchDay.PlayMatchdayAsync(state, teams, 1, humanFixture, humanResult);

            var reloaded = await _saveGame.GetFixturesAsync(1);
            Assert.All(reloaded.Where(f => f.Matchday == 1), f => Assert.True(f.Played));
            Assert.Equal(1, state.MatchdayIndex);
            Assert.True(state.CurrentDate > startDate);
            Assert.Equal(1, manager.Statistics!.MatchesPlayed);
        }

        [Fact]
        public async Task PlayingAMatchday_ConvertsSubstitutedOffHumanPlayers_ToOnBench()
        {
            var (leagues, teams) = BuildUniverse();
            var manager = teams.First(t => t.LeagueTier == 4);
            var state = await _saveGame.StartNewCareerAsync(
                "Test", 1, leagues, teams, manager, new DateTime(2026, 8, 1));

            var fixtures = await _saveGame.GetFixturesAsync(1);
            var humanFixture = fixtures.First(f => f.Matchday == 1 &&
                (f.HomeTeamId == manager.Id || f.AwayTeamId == manager.Id));

            var humanResult = SimulateHumanFixture(teams, humanFixture);

            // A substitution made during the match leaves the outgoing player SubstitutedOff
            // (Match.TrySubstitute) - PlayMatchdayAsync must convert this back to OnBench for
            // the human team right after full time, or the Lineup page's Bench/Reserves would
            // simply lose that player until the next matchday prep/calendar tick.
            var subbedOff = manager.Players.First(p => p.Status == PlayerStatus.InStartingXI);
            subbedOff.Status = PlayerStatus.SubstitutedOff;

            await _matchDay.PlayMatchdayAsync(state, teams, 1, humanFixture, humanResult);

            Assert.Equal(PlayerStatus.OnBench, subbedOff.Status);
        }

        [Fact]
        public async Task AdvancingSeason_AppliesPromotionRelegation_ResetsStatsAndFixtures()
        {
            var (leagues, teams) = BuildUniverse();
            var manager = teams.First(t => t.LeagueTier == 4);
            var state = await _saveGame.StartNewCareerAsync(
                "Test", 1, leagues, teams, manager, new DateTime(2026, 8, 1));

            // Mark the whole season as played (results are irrelevant to the rollover mechanics).
            var fixtures = await _saveGame.GetFixturesAsync(1);
            foreach (var f in fixtures)
            {
                f.Played = true;
                f.HomeGoals = 1;
                f.AwayGoals = 0;
                await _fixtureRepo.SaveAsync(f);
            }

            var endResult = await _saveGame.AdvanceToNextSeasonAsync(state, teams, _career);

            Assert.Equal(2, state.Season);
            Assert.Equal(0, state.MatchdayIndex);
            Assert.True(_career.Points >= 25);
            Assert.NotNull(endResult);

            var newFixtures = await _saveGame.GetFixturesAsync(2);
            Assert.NotEmpty(newFixtures);
            Assert.All(teams, t => Assert.Equal(2, t.Statistics!.Season));
            Assert.All(teams, t => Assert.Equal(0, t.Statistics!.MatchesPlayed));

            // Every league still has 6 teams after promotion/relegation swaps.
            foreach (var tier in Enumerable.Range(1, 4))
                Assert.Equal(6, teams.Count(t => t.LeagueTier == tier));
        }

        [Fact]
        public async Task AdvanceToNextSeasonAsync_ManagerFinishesFirst_RecordsTrophy()
        {
            var (leagues, teams) = BuildUniverse();
            var manager = teams.First(t => t.LeagueTier == 4);
            var state = await _saveGame.StartNewCareerAsync(
                "Test", 1, leagues, teams, manager, new DateTime(2026, 8, 1));

            // Manager gewinnt jedes eigene Spiel (Heim+Auswärts) - garantiert Platz 1, da jeder
            // andere Tier-4-Verein mindestens 2 Niederlagen (gegen den Manager) hat.
            var fixtures = await _saveGame.GetFixturesAsync(1);
            foreach (var f in fixtures)
            {
                f.Played = true;
                if (f.HomeTeamId == manager.Id) { f.HomeGoals = 3; f.AwayGoals = 0; }
                else if (f.AwayTeamId == manager.Id) { f.HomeGoals = 0; f.AwayGoals = 3; }
                await _fixtureRepo.SaveAsync(f);
            }

            var result = await _saveGame.AdvanceToNextSeasonAsync(state, teams, _career);

            Assert.Equal(1, result.ManagerFinalPosition);
            var trophies = await _saveGame.GetTrophiesForTeamAsync(manager.Id);
            var trophy = Assert.Single(trophies);
            Assert.Equal(TrophyType.MeisterLiga4, trophy.Type);
            Assert.Equal(1, trophy.Count);
        }

        [Fact]
        public async Task AdvanceToNextSeasonAsync_ManagerDoesNotFinishFirst_DoesNotRecordTrophy()
        {
            var (leagues, teams) = BuildUniverse();
            var manager = teams.First(t => t.LeagueTier == 4);
            var state = await _saveGame.StartNewCareerAsync(
                "Test", 1, leagues, teams, manager, new DateTime(2026, 8, 1));

            // Manager verliert jedes eigene Spiel.
            var fixtures = await _saveGame.GetFixturesAsync(1);
            foreach (var f in fixtures)
            {
                f.Played = true;
                if (f.HomeTeamId == manager.Id) { f.HomeGoals = 0; f.AwayGoals = 3; }
                else if (f.AwayTeamId == manager.Id) { f.HomeGoals = 3; f.AwayGoals = 0; }
                await _fixtureRepo.SaveAsync(f);
            }

            var result = await _saveGame.AdvanceToNextSeasonAsync(state, teams, _career);

            Assert.True(result.ManagerFinalPosition > 1);
            var trophies = await _saveGame.GetTrophiesForTeamAsync(manager.Id);
            Assert.Empty(trophies);
        }

        [Fact]
        public async Task PlayingAMatchday_AssignsAndAppliesTrainingFocus_ForHumanAndComTeams()
        {
            var (leagues, teams) = BuildUniverse();
            var manager = teams.First(t => t.LeagueTier == 4);
            var state = await _saveGame.StartNewCareerAsync(
                "Test", 1, leagues, teams, manager, new DateTime(2026, 8, 1));

            var fixtures = await _saveGame.GetFixturesAsync(1);
            var humanFixture = fixtures.First(f => f.Matchday == 1 &&
                (f.HomeTeamId == manager.Id || f.AwayTeamId == manager.Id));
            var comOpponent = teams.First(t => t.Id != manager.Id &&
                (t.Id == humanFixture.HomeTeamId || t.Id == humanFixture.AwayTeamId));

            var humanResult = SimulateHumanFixture(teams, humanFixture);
            await _matchDay.PlayMatchdayAsync(state, teams, 1, humanFixture, humanResult);

            // COM team gets an auto-assigned focus (individual + team), unlike a human team that
            // sets its own focus manually - here the manager never set one, so it stays null.
            Assert.All(comOpponent.Players, p => Assert.NotNull(p.CurrentTrainingFocus));
            Assert.NotNull(comOpponent.TeamTrainingFocus);
            Assert.All(manager.Players, p => Assert.Null(p.CurrentTrainingFocus));
        }

        [Fact]
        public async Task PlayingAMatchday_PersistsSeasonPlayerStats()
        {
            var (leagues, teams) = BuildUniverse();
            var manager = teams.First(t => t.LeagueTier == 4);
            var state = await _saveGame.StartNewCareerAsync(
                "Test", 1, leagues, teams, manager, new DateTime(2026, 8, 1));

            var fixtures = await _saveGame.GetFixturesAsync(1);
            var humanFixture = fixtures.First(f => f.Matchday == 1 &&
                (f.HomeTeamId == manager.Id || f.AwayTeamId == manager.Id));

            var humanResult = SimulateHumanFixture(teams, humanFixture);
            await _matchDay.PlayMatchdayAsync(state, teams, 1, humanFixture, humanResult);

            Assert.NotEmpty(humanResult.PlayerMatchStats);
            var (playerId, matchStats) = humanResult.PlayerMatchStats.First();

            var playerRepo = new PlayerRepository(_db);
            var seasonStats = (await playerRepo.GetPlayerStatsAsync(playerId, 1)).Single();

            Assert.Equal(1, seasonStats.Appearances);
            Assert.Equal(matchStats.Goals, seasonStats.Goals);
            Assert.Equal(matchStats.Assists, seasonStats.Assists);
            Assert.Equal(matchStats.Passes, seasonStats.Passes);
            Assert.Equal(matchStats.Tackles, seasonStats.Tackles);
            Assert.Equal(matchStats.HeaderDuels, seasonStats.HeaderDuels);
            Assert.InRange(seasonStats.Rating, 1.0, 6.0);
        }

        [Fact]
        public async Task PlayingAMatchday_AccumulatesSetPieceAndPassingTeamStats()
        {
            var (leagues, teams) = BuildUniverse();
            var manager = teams.First(t => t.LeagueTier == 4);
            var state = await _saveGame.StartNewCareerAsync(
                "Test", 1, leagues, teams, manager, new DateTime(2026, 8, 1));

            var fixtures = await _saveGame.GetFixturesAsync(1);
            var humanFixture = fixtures.First(f => f.Matchday == 1 &&
                (f.HomeTeamId == manager.Id || f.AwayTeamId == manager.Id));
            var home = teams.First(t => t.Id == humanFixture.HomeTeamId);
            var away = teams.First(t => t.Id == humanFixture.AwayTeamId);

            var humanResult = SimulateHumanFixture(teams, humanFixture);
            await _matchDay.PlayMatchdayAsync(state, teams, 1, humanFixture, humanResult);

            Assert.Equal(humanResult.MatchStatsHome.Corners, home.Statistics!.Corners);
            Assert.Equal(humanResult.MatchStatsHome.FreeKicks, home.Statistics!.FreeKicks);
            Assert.Equal(humanResult.MatchStatsHome.Penaltys, home.Statistics!.Penaltys);
            Assert.Equal(humanResult.MatchStatsHome.Offsides, home.Statistics!.Offsides);
            Assert.Equal(humanResult.MatchStatsHome.Passes, home.Statistics!.Passes);
            Assert.Equal(humanResult.MatchStatsHome.SuccessfulPasses, home.Statistics!.SuccessfulPasses);

            Assert.Equal(humanResult.MatchStatsAway.Corners, away.Statistics!.Corners);
            Assert.Equal(humanResult.MatchStatsAway.Passes, away.Statistics!.Passes);
        }

        [Fact]
        public async Task PlayingAMatchday_WithFinanceServiceWired_ChangesTeamBalance()
        {
            var (leagues, teams) = BuildUniverse();
            foreach (var team in teams)
            {
                team.Stadium = new Stadium
                {
                    SeatingCapacity = 10_000, StandingCapacity = 2_000, LogeCapacity = 100,
                    SeatPrice = 20, StandingPrice = 10, LogePrice = 80, ComfortLevel = 3,
                    MaintenanceCosts = 100_000,
                };
                team.Finances = new Finances { CurrentBalance = 500_000 };
            }

            var manager = teams.First(t => t.LeagueTier == 4);
            var state = await _saveGame.StartNewCareerAsync(
                "Test", 1, leagues, teams, manager, new DateTime(2026, 8, 1));

            var financeService = new FinanceService(
                new SponsorRepository(_db), new SponsorshipRepository(_db), new ContractRepository(_db));
            var matchDayWithFinance = new MatchDayService(
                _fixtureRepo, new TeamRepository(_db), new PlayerRepository(_db), financeService, random: new Random(1));

            var fixtures = await _saveGame.GetFixturesAsync(1);
            var humanFixture = fixtures.First(f => f.Matchday == 1 &&
                (f.HomeTeamId == manager.Id || f.AwayTeamId == manager.Id));

            var balanceBefore = manager.Finances!.CurrentBalance;
            var humanResult = SimulateHumanFixture(teams, humanFixture);
            await matchDayWithFinance.PlayMatchdayAsync(state, teams, 1, humanFixture, humanResult);

            Assert.NotEqual(balanceBefore, manager.Finances!.CurrentBalance);
        }
    }
}
