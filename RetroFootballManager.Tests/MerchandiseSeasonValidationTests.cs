using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    // Drives a full (small) season through the real MatchDayService/FinanceService/
    // MerchandiseService/ClubMembershipService wiring - same harness pattern as
    // MatchDayIntegrationTests (small 6-team-per-tier leagues so a full home+away
    // round-robin, i.e. a structurally COMPLETE season, still runs fast). Purpose: confirm
    // the Merchandise shop + membership campaigns (human AND AI, difficulty-scaled) never
    // "ausarten" over a whole season - bounded membership growth, proportionate income/
    // expense, no runaway AI spending. Difficulty is set to Hard, the most AI-active setting,
    // as a deliberate stress test of the upper bound.
    public class MerchandiseSeasonValidationTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private readonly string _careerPath;
        private AppDatabase _db = null!;
        private SaveGameService _saveGame = null!;
        private MatchDayService _matchDay = null!;
        private CareerService _career = null!;

        public MerchandiseSeasonValidationTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_merchseason_{Guid.NewGuid():N}.db3");
            _careerPath = Path.Combine(Path.GetTempPath(), $"rfm_merchseason_{Guid.NewGuid():N}.json");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            _saveGame = new SaveGameService(_db);
            var fixtureRepo = new FixtureRepository(_db);
            var finance = new FinanceService(new SponsorRepository(_db), new SponsorshipRepository(_db), new ContractRepository(_db));
            var merchandise = new MerchandiseService(new MerchandiseRepository(_db));
            _matchDay = new MatchDayService(
                fixtureRepo, new TeamRepository(_db), new PlayerRepository(_db), finance,
                merchandise: merchandise, random: new Random(1));
            _career = new CareerService(_careerPath);
            await _saveGame.InitializeAsync();
        }

        public async Task DisposeAsync()
        {
            await _saveGame.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
            if (File.Exists(_careerPath)) File.Delete(_careerPath);
        }

        // 6 teams/tier x 4 tiers - every team plays every other team in its tier home+away
        // (a structurally complete season), just smaller than the real 18-team leagues so the
        // test runs fast. Half the teams in each tier get a Director of Football, so the AI
        // campaign path (not just the passive restock path) is genuinely exercised.
        private static (List<League> Leagues, List<Team> Teams) BuildUniverse(Random rng)
        {
            var leagues = new List<League>();
            var teams = new List<Team>();

            for (int tier = 1; tier <= 4; tier++)
            {
                leagues.Add(new League { Name = $"Liga {tier}", Tier = tier, Season = 1 });
                for (int i = 0; i < 6; i++)
                {
                    var players = PlayerGenerator.GenerateSquad(Nationality.Germany, 70 - tier * 8, squadSize: 16, random: rng);
                    var (members, fee) = ClubMembershipService.ForTierAndRating(tier, players.Average(p => p.Rating));

                    var team = new Team
                    {
                        Name = $"T{tier}-{i}",
                        ShortName = $"T{tier}{i}",
                        LeagueTier = tier,
                        Statistics = new TeamStats { Season = 1 },
                        Stadium = new Stadium
                        {
                            SeatingCapacity = 8_000, StandingCapacity = 2_000, LogeCapacity = 50,
                            SeatPrice = 15, StandingPrice = 8, LogePrice = 60,
                            MaintenanceCosts = 50_000, ComfortLevel = 2, MerchandiseLevel = 2,
                        },
                        Finances = new Finances { CurrentBalance = 200_000, ClubMembers = members, MembershipFeePerMember = fee },
                    };
                    team.Players.AddRange(players);

                    if (i % 2 == 0)
                        team.Employees.Add(new Employee { EmployeeType = EmployeeType.DirectorOfFootball, Rating = 60, FinancialManagement = 60 });

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

        private static readonly Dictionary<int, int> HardCapByTier = new() { [1] = 200_000, [2] = 85_000, [3] = 30_000, [4] = 15_000 };

        [Theory]
        [InlineData(42)]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(99)]
        public async Task FullSeason_MerchandiseAndMembershipCampaigns_StayWithinRealisticBounds(int seed)
        {
            var rng = new Random(seed);
            var (leagues, teams) = BuildUniverse(rng);
            var manager = teams.First(t => t.LeagueTier == 4);
            var state = await _saveGame.StartNewCareerAsync("Test", 1, leagues, teams, manager, new DateTime(2026, 8, 1));
            state.Difficulty = Difficulty.Hard;

            var startingMembersByTeam = teams.ToDictionary(t => t.Id, t => t.Finances!.ClubMembers);

            var fixtures = await _saveGame.GetFixturesAsync(1);
            int totalMatchdays = fixtures.Max(f => f.Matchday);

            for (int matchday = 1; matchday <= totalMatchdays; matchday++)
            {
                var currentFixtures = await _saveGame.GetFixturesAsync(1);
                var humanFixture = currentFixtures.FirstOrDefault(f => f.Matchday == matchday &&
                    (f.HomeTeamId == manager.Id || f.AwayTeamId == manager.Id));
                if (humanFixture is null)
                    continue;

                var humanResult = SimulateHumanFixture(teams, humanFixture);
                await _matchDay.PlayMatchdayAsync(state, teams, matchday, humanFixture, humanResult);
            }

            // 1) Membership never blew past the hard cap, even with a full season of back-to-
            //    back AI (and the human's own) campaigns at the most AI-active difficulty.
            foreach (var team in teams)
                Assert.True(team.Finances!.ClubMembers <= HardCapByTier[team.LeagueTier],
                    $"{team.Name} (Tier {team.LeagueTier}) ended with {team.Finances.ClubMembers:N0} members - exceeds the hard cap.");

            // 2) Merchandise income/expense stayed proportionate to the club's own fanbase -
            //    generous headroom (200 €/member across the whole season), a genuine runaway
            //    shop would blow past this by a wide margin.
            foreach (var team in teams)
            {
                int members = Math.Max(team.Finances!.ClubMembers, 100);
                Assert.True(team.Finances.MerchandiseArticleIncome < members * 200,
                    $"{team.Name}: merchandise income {team.Finances.MerchandiseArticleIncome:N0} € looks disproportionate to {members:N0} members.");
                Assert.True(team.Finances.MerchandiseArticleExpense < members * 200,
                    $"{team.Name}: merchandise expense {team.Finances.MerchandiseArticleExpense:N0} € looks disproportionate to {members:N0} members.");
            }

            // 3) No team's balance collapsed into an absurd, unbounded negative purely from
            //    Merchandise/campaign spending (AI discretionary spend is caution-gated, stock
            //    purchases are always balance-checked before committing).
            foreach (var team in teams)
                Assert.True(team.Finances!.CurrentBalance > -2_000_000,
                    $"{team.Name} ended at {team.Finances.CurrentBalance:N0} € - unrealistically deep in the red.");

            // 4) The whole DoF -> campaign -> drip pipeline genuinely fired for at least one
            //    club over the season (confirms the gating isn't simply blocking everything).
            bool anyGrowth = teams.Any(t => t.Finances!.ClubMembers > startingMembersByTeam[t.Id]);
            Assert.True(anyGrowth, "No team's membership grew at all over the season - campaigns never fired.");
        }
    }
}
