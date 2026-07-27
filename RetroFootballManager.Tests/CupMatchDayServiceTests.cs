using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class CupMatchDayServiceTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private CupTieRepository _cupTieRepo = null!;
        private TeamRepository _teamRepo = null!;
        private CupMatchDayService _service = null!;

        public CupMatchDayServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_cup_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _cupTieRepo = new CupTieRepository(_db);
            _teamRepo = new TeamRepository(_db);
            _service = new CupMatchDayService(_cupTieRepo, _teamRepo, new Random(1));
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        private static CupTie NewTie(int homeId, int awayId, int round = CupDrawService.RoundQuarterFinal) => new()
        {
            CompetitionType = CompetitionType.GermanCup,
            Season = 1,
            Round = round,
            MatchNumberInRound = 1,
            HomeTeamId = homeId,
            AwayTeamId = awayId,
            Date = new DateTime(2026, 9, 1),
        };

        [Fact]
        public async Task PlayCupRoundAsync_SimulatesAiTies_MarksPlayed()
        {
            var home = TestHelpers.CreateTeam("Heim", baseRating: 60);
            home.Id = 1;
            var away = TestHelpers.CreateTeam("Gast", baseRating: 60);
            away.Id = 2;
            var tie = NewTie(1, 2);

            var result = await _service.PlayCupRoundAsync([home, away], [tie], humanTie: null, humanResult: null);

            Assert.True(result[0].Played);
            Assert.False(result[0].HomeGoals < 0);
        }

        [Fact]
        public async Task PlayCupRoundAsync_HumanTie_UsesSuppliedResult()
        {
            var home = TestHelpers.CreateTeam("Heim", baseRating: 60);
            home.Id = 1;
            var away = TestHelpers.CreateTeam("Gast", baseRating: 60);
            away.Id = 2;
            var tie = NewTie(1, 2);

            var humanResult = new MatchResult { HomeGoals = 3, AwayGoals = 1 };
            var result = await _service.PlayCupRoundAsync([home, away], [tie], tie, humanResult);

            Assert.Equal(3, result[0].HomeGoals);
            Assert.Equal(1, result[0].AwayGoals);
            Assert.False(result[0].WentToPenalties);
        }

        [Fact]
        public async Task PlayCupRoundAsync_DrawAfter90Minutes_ResolvesViaPenalties()
        {
            var home = TestHelpers.CreateTeam("Heim", baseRating: 60);
            home.Id = 1;
            var away = TestHelpers.CreateTeam("Gast", baseRating: 60);
            away.Id = 2;
            var tie = NewTie(1, 2);

            var drawResult = new MatchResult { HomeGoals = 1, AwayGoals = 1 };
            var result = await _service.PlayCupRoundAsync([home, away], [tie], tie, drawResult);

            Assert.True(result[0].WentToPenalties);
            Assert.NotNull(result[0].PenaltyHomeGoals);
            Assert.NotNull(result[0].PenaltyAwayGoals);
            Assert.NotEqual(result[0].PenaltyHomeGoals, result[0].PenaltyAwayGoals);
        }

        [Fact]
        public async Task PlayCupRoundAsync_Final_DoesNotPermanentlyChangeHomeTeamStadium()
        {
            var originalStadium = new Stadium { Name = "Heimstadion", HomeAdvantage = 80 };
            var home = TestHelpers.CreateTeam("Heim", baseRating: 60, stadium: originalStadium);
            home.Id = 1;
            var away = TestHelpers.CreateTeam("Gast", baseRating: 60);
            away.Id = 2;
            var tie = NewTie(1, 2, round: CupDrawService.RoundFinal);

            await _service.PlayCupRoundAsync([home, away], [tie], humanTie: null, humanResult: null);

            Assert.Same(originalStadium, home.Stadium);
        }

        [Fact]
        public async Task PlayCupRoundAsync_PersistsTiesAndTouchedTeams()
        {
            var home = TestHelpers.CreateTeam("Heim", baseRating: 60);
            home.Id = 1;
            var away = TestHelpers.CreateTeam("Gast", baseRating: 60);
            away.Id = 2;
            await _teamRepo.SaveTeamAsync(home);
            await _teamRepo.SaveTeamAsync(away);
            var tie = NewTie(1, 2);

            await _service.PlayCupRoundAsync([home, away], [tie], humanTie: null, humanResult: null);

            var persisted = await _cupTieRepo.GetByRoundAsync(1, CompetitionType.GermanCup, CupDrawService.RoundQuarterFinal);
            Assert.Single(persisted);
            Assert.True(persisted[0].Played);
        }

        private static CupTie NewLeg(int homeId, int awayId, int legNumber, int round = CupDrawService.RoundLastSixteen) => new()
        {
            CompetitionType = CompetitionType.ChampionsLeague,
            Season = 1,
            Round = round,
            MatchNumberInRound = 1,
            LegNumber = legNumber,
            HomeTeamId = homeId,
            AwayTeamId = awayId,
            Date = new DateTime(2026, 9, 1),
        };

        [Fact]
        public async Task PlayCupRoundAsync_GroupStageTie_DrawNeverGoesToPenalties()
        {
            var home = TestHelpers.CreateTeam("Heim", baseRating: 60);
            home.Id = 1;
            var away = TestHelpers.CreateTeam("Gast", baseRating: 60);
            away.Id = 2;
            var tie = NewLeg(1, 2, CupTie.LegNone, round: 0);

            var drawResult = new MatchResult { HomeGoals = 1, AwayGoals = 1 };
            var result = await _service.PlayCupRoundAsync([home, away], [tie], tie, drawResult);

            Assert.False(result[0].WentToPenalties);
        }

        [Fact]
        public async Task PlayCupRoundAsync_FirstLegDraw_NeverGoesToPenalties()
        {
            var home = TestHelpers.CreateTeam("Heim", baseRating: 60);
            home.Id = 1;
            var away = TestHelpers.CreateTeam("Gast", baseRating: 60);
            away.Id = 2;
            var tie = NewLeg(1, 2, CupTie.LegFirst);

            var drawResult = new MatchResult { HomeGoals = 1, AwayGoals = 1 };
            var result = await _service.PlayCupRoundAsync([home, away], [tie], tie, drawResult);

            Assert.False(result[0].WentToPenalties);
        }

        [Fact]
        public async Task PlayCupRoundAsync_SecondLegAggregateTied_GoesToPenalties()
        {
            var home = TestHelpers.CreateTeam("Heim", baseRating: 60);
            home.Id = 1;
            var away = TestHelpers.CreateTeam("Gast", baseRating: 60);
            away.Id = 2;

            var firstLeg = NewLeg(1, 2, CupTie.LegFirst);
            firstLeg.HomeGoals = 1; firstLeg.AwayGoals = 1; firstLeg.Played = true;
            var secondLeg = NewLeg(2, 1, CupTie.LegSecond);

            var drawResult = new MatchResult { HomeGoals = 0, AwayGoals = 0 }; // Aggregat 1:1
            var result = await _service.PlayCupRoundAsync(
                [home, away], [secondLeg], secondLeg, drawResult, firstLegTies: [firstLeg]);

            Assert.True(result[0].WentToPenalties);
        }

        [Fact]
        public async Task PlayCupRoundAsync_SecondLegAggregateNotTied_NoPenalties_EvenIfLegItselfDrawn()
        {
            var home = TestHelpers.CreateTeam("Heim", baseRating: 60);
            home.Id = 1;
            var away = TestHelpers.CreateTeam("Gast", baseRating: 60);
            away.Id = 2;

            var firstLeg = NewLeg(1, 2, CupTie.LegFirst);
            firstLeg.HomeGoals = 2; firstLeg.AwayGoals = 0; firstLeg.Played = true;
            var secondLeg = NewLeg(2, 1, CupTie.LegSecond);

            var drawResult = new MatchResult { HomeGoals = 1, AwayGoals = 1 }; // Leg selbst unentschieden, Aggregat 2:2? -> Home(=2)+Away leg2(=1)=... check below
            var result = await _service.PlayCupRoundAsync(
                [home, away], [secondLeg], secondLeg, drawResult, firstLegTies: [firstLeg]);

            // Aggregat: Team1 = firstLeg.HomeGoals(2) + secondLeg.AwayGoals(1) = 3;
            // Team2 = firstLeg.AwayGoals(0) + secondLeg.HomeGoals(1) = 1 -> nicht ausgeglichen.
            Assert.False(result[0].WentToPenalties);
        }

        [Fact]
        public async Task PlayCupRoundAsync_PersistsPlayerStatsUnderCorrectCompetition()
        {
            var home = TestHelpers.CreateTeam("Heim", baseRating: 60);
            home.Id = 1;
            var away = TestHelpers.CreateTeam("Gast", baseRating: 60);
            away.Id = 2;
            var playerRepo = new PlayerRepository(_db);
            var service = new CupMatchDayService(_cupTieRepo, _teamRepo, new Random(1), players: playerRepo);
            var tie = NewLeg(1, 2, CupTie.LegNone, round: 0);

            await service.PlayCupRoundAsync([home, away], [tie], humanTie: null, humanResult: null);

            var cupStats = await playerRepo.GetStatsByCompetitionAsync(season: 1, CompetitionType.ChampionsLeague);
            Assert.NotEmpty(cupStats);

            var playerId = cupStats[0].PlayerId;
            var leagueStats = await playerRepo.GetPlayerStatsAsync(playerId, season: 1);
            Assert.Empty(leagueStats);
        }
    }
}
