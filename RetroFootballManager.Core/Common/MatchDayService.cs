using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public record MatchdayGame(
        int LeagueTier,
        int HomeTeamId,
        int AwayTeamId,
        string HomeName,
        string AwayName,
        int HomeGoals,
        int AwayGoals);

    public record MatchdaySummary(int Matchday, List<MatchdayGame> Games);

    // Plays out a full matchday: the human's match is played live in the UI (result is
    // passed in), all other fixtures across all leagues are simulated immediately (AI on
    // both sides). Results flow into Fixtures and TeamStats, then time advances to the
    // next matchday.
    public class MatchDayService
    {
        private readonly FixtureRepository _fixtures;
        private readonly TeamRepository _teams;
        private readonly PlayerRepository _players;
        private readonly FinanceService? _finance;
        private readonly AiManagerService? _aiManager;
        private readonly MessageService? _messages;
        private readonly Random _random;

        public MatchDayService(
            FixtureRepository fixtures, TeamRepository teams, PlayerRepository players,
            FinanceService? finance = null, AiManagerService? aiManager = null,
            MessageService? messages = null, Random? random = null)
        {
            _fixtures = fixtures;
            _teams = teams;
            _players = players;
            _finance = finance;
            _aiManager = aiManager;
            _messages = messages;
            _random = random ?? Random.Shared;
        }

        // Restores full fitness and clears previous injuries/suspensions before a match,
        // WITHOUT touching the chosen lineup (used for the human team so their manual XI is
        // kept). currentDate == null keeps the old "always heal" behaviour (tests); when
        // given, a player only recovers once InjuredUntil is reached - real injury duration.
        public static void RecoverForMatch(Team team, DateTime? currentDate = null)
        {
            foreach (var p in team.Players)
            {
                p.Fitness = 100;
                if (p.Status == PlayerStatus.Injured)
                {
                    bool stillOut = currentDate.HasValue && p.InjuredUntil.HasValue && p.InjuredUntil.Value > currentDate.Value;
                    if (stillOut)
                        continue;
                    p.Status = PlayerStatus.Available;
                    p.InjuredUntil = null;
                }
                else if (p.Status is PlayerStatus.Suspended or PlayerStatus.SubstitutedOff)
                {
                    p.Status = p.Status == PlayerStatus.SubstitutedOff ? PlayerStatus.OnBench : PlayerStatus.Available;
                }
            }
        }

        // Recovers the team and then auto-picks a position-correct XI (used for COM teams).
        public static void PrepareForMatch(Team team, DateTime? currentDate = null)
        {
            RecoverForMatch(team, currentDate);
            LineupSelector.SelectLineup(team);
        }

        // Sends a PlayerRecovered message for every human-team player whose InjuredUntil has
        // been reached - call BEFORE RecoverForMatch clears the flag. Static so any caller
        // (ViewModels, CalendarAdvanceService) can use it without a MatchDayService instance.
        public static async Task NotifyInjuryRecoveriesAsync(MessageService? messages, Team humanTeam, DateTime currentDate)
        {
            if (messages is null)
                return;

            foreach (var p in humanTeam.Players.Where(p =>
                p.Status == PlayerStatus.Injured && p.InjuredUntil.HasValue && p.InjuredUntil.Value <= currentDate))
            {
                await messages.SendAsync(MessageType.PlayerRecovered, "Spieler wieder fit",
                    $"{p.Name} ist wieder einsatzbereit.", currentDate, humanTeam.Id, p.Id);
            }
        }

        public async Task<MatchdaySummary> PlayMatchdayAsync(
            GameState state,
            IReadOnlyList<Team> teams,
            int matchday,
            Fixture humanFixture,
            MatchResult humanResult)
        {
            var teamById = teams.ToDictionary(t => t.Id);
            var names = teams.ToDictionary(t => t.Id, t => t.Name);
            var fixtures = await _fixtures.GetByMatchdayAsync(state.Season, matchday);

            var games = new List<MatchdayGame>();
            var touchedTeamIds = new HashSet<int>();
            var matchResults = new List<MatchResult>();
            var teamFixture = new Dictionary<int, (Fixture Fixture, bool IsHome)>();
            var preMatchStandingsByTier = new Dictionary<int, List<StandingRow>>();

            async Task<List<StandingRow>> GetPreMatchStandingsAsync(int leagueTier)
            {
                if (preMatchStandingsByTier.TryGetValue(leagueTier, out var cached))
                    return cached;

                var seasonFixtures = await _fixtures.GetByLeagueAsync(state.Season, leagueTier);
                var standings = StandingsCalculator.Calculate(seasonFixtures, names);
                preMatchStandingsByTier[leagueTier] = standings;
                return standings;
            }

            foreach (var fixture in fixtures.OrderBy(f => f.LeagueTier).ThenBy(f => f.Date))
            {
                var home = teamById[fixture.HomeTeamId];
                var away = teamById[fixture.AwayTeamId];

                if (fixture.Id == humanFixture.Id)
                {
                    ApplyResult(fixture, humanResult, home, away);
                    matchResults.Add(humanResult);

                    if (_messages is not null)
                        await NotifyInjuriesAsync(_messages, humanResult, home, away, state.ManagerTeamId, fixture.Date);
                }
                else
                {
                    PrepareForMatch(home, state.CurrentDate);
                    PrepareForMatch(away, state.CurrentDate);

                    // AI tactic adjustment: both sides adapt playing style/orientation to
                    // their opponent before kickoff (no analyst needed, see M5f).
                    if (_aiManager is not null)
                    {
                        var tierTeams = teams.Where(t => t.LeagueTier == home.LeagueTier).ToList();
                        var standings = await GetPreMatchStandingsAsync(home.LeagueTier);
                        TacticAiService.ApplyPreMatchTactic(home, away, standings, tierTeams, state.Difficulty, _random);
                        TacticAiService.ApplyPreMatchTactic(away, home, standings, tierTeams, state.Difficulty, _random);
                    }

                    var match = new Match(home, away, _random)
                    {
                        HomeCoach = new AiMatchCoach(),
                        AwayCoach = new AiMatchCoach(),
                    };
                    var result = match.Simulate();
                    ApplyResult(fixture, result, home, away);
                    matchResults.Add(result);
                }

                touchedTeamIds.Add(home.Id);
                touchedTeamIds.Add(away.Id);
                teamFixture[home.Id] = (fixture, true);
                teamFixture[away.Id] = (fixture, false);
                games.Add(new MatchdayGame(
                    fixture.LeagueTier, home.Id, away.Id, home.Name, away.Name,
                    fixture.HomeGoals, fixture.AwayGoals));
            }

            // Weekly training tick for every team that played this matchday - the human's own
            // focus choices progress at full pace; every COM team trains too (auto-assigning a
            // focus if it doesn't have one), scaled by difficulty and by that team's own
            // morale/fitness so a struggling AI side doesn't simply out-develop everyone.
            foreach (var id in touchedTeamIds)
            {
                var team = teamById[id];
                bool isHuman = id == state.ManagerTeamId;
                TrainingService.ApplyWeeklyTraining(team, isHuman, state.Difficulty, _random);

                if (!isHuman && _aiManager is not null)
                    await _aiManager.RunWeeklyTickAsync(team, state.Season, state.CurrentDate, state.Difficulty, _random, teamById, state.ManagerTeamId);
            }

            if (_aiManager is not null)
                await _aiManager.ReturnExpiredLoansAsync(state.CurrentDate, teamById);

            if (_finance is not null)
                await ApplyFinanceAsync(state, teamById, names, fixtures, touchedTeamIds, teamFixture);

            // Persist: changed fixtures and all involved teams (TeamStats + career minutes).
            foreach (var fixture in fixtures)
                await _fixtures.SaveAsync(fixture);
            foreach (var id in touchedTeamIds)
                await _teams.SaveTeamAsync(teamById[id], includeYouth: false);

            await PersistPlayerStatsAsync(matchResults, state.Season);

            AdvanceDate(state, fixtures, matchday);

            return new MatchdaySummary(matchday, games);
        }

        // Books ticket/sponsor/merchandise income and stadium/staff costs for every
        // involved team. For the standings/form per league, already-played season fixtures
        // are fetched from the DB and overlaid with this matchday's freshly simulated
        // results (not yet persisted at this point).
        private async Task ApplyFinanceAsync(
            GameState state,
            IReadOnlyDictionary<int, Team> teamById,
            IReadOnlyDictionary<int, string> names,
            IReadOnlyList<Fixture> todaysFixtures,
            IReadOnlyCollection<int> touchedTeamIds,
            IReadOnlyDictionary<int, (Fixture Fixture, bool IsHome)> teamFixture)
        {
            var standingsByTier = new Dictionary<int, List<StandingRow>>();

            async Task<List<StandingRow>> GetStandingsAsync(int leagueTier)
            {
                if (standingsByTier.TryGetValue(leagueTier, out var cached))
                    return cached;

                var seasonFixtures = await _fixtures.GetByLeagueAsync(state.Season, leagueTier);
                var todaysByTier = todaysFixtures.Where(f => f.LeagueTier == leagueTier).ToDictionary(f => f.Id);
                foreach (var f in seasonFixtures)
                {
                    if (todaysByTier.TryGetValue(f.Id, out var updated))
                    {
                        f.HomeGoals = updated.HomeGoals;
                        f.AwayGoals = updated.AwayGoals;
                        f.Played = updated.Played;
                    }
                }

                var standings = StandingsCalculator.Calculate(seasonFixtures, names);
                standingsByTier[leagueTier] = standings;
                return standings;
            }

            foreach (var id in touchedTeamIds)
            {
                var team = teamById[id];
                var (fixture, isHome) = teamFixture[id];
                bool won = isHome ? fixture.HomeGoals > fixture.AwayGoals : fixture.AwayGoals > fixture.HomeGoals;
                int opponentTeamId = isHome ? fixture.AwayTeamId : fixture.HomeTeamId;
                int opponentTierRank = teamById[opponentTeamId].LeagueTier;

                var standings = await GetStandingsAsync(team.LeagueTier);
                await _finance!.ApplyMatchdayFinanceAsync(team, isHome, won, standings, opponentTierRank, state.CurrentDate);
                bool isHumanTeam = id == state.ManagerTeamId;
                await _finance.ApplyMonthlySettlementAsync(team, state.CurrentDate, sendMessage: isHumanTeam);

                if (isHumanTeam)
                    await _finance.CheckFinanceWarningAsync(team, state.CurrentDate);
            }
        }

        private static void ApplyResult(Fixture fixture, MatchResult result, Team home, Team away)
        {
            fixture.HomeGoals = result.HomeGoals;
            fixture.AwayGoals = result.AwayGoals;
            fixture.Played = true;

            result.ApplyToTeamStats(home.Statistics!, away.Statistics!);
            result.ApplyInjuryDurations(fixture.Date);
            ApplyCareerMinutes(result, home, away);
        }

        // Sends a PlayerInjured message for every injured human-team player, including their
        // expected return date - static so CupMatchDayService/FriendlyService/ViewModels can reuse it.
        public static async Task NotifyInjuriesAsync(
            MessageService messages, MatchResult result, Team home, Team away, int humanTeamId, DateTime matchDate)
        {
            if (result.InjuredPlayers.Count == 0)
                return;

            var humanTeam = home.Id == humanTeamId ? home : away.Id == humanTeamId ? away : null;
            if (humanTeam is null)
                return;

            foreach (var player in result.InjuredPlayers.Where(p => humanTeam.Players.Any(hp => hp.Id == p.Id)))
            {
                string until = player.InjuredUntil.HasValue ? player.InjuredUntil.Value.ToString("dd.MM.yyyy") : "unbekannt";
                await messages.SendAsync(MessageType.PlayerInjured, "Spieler verletzt",
                    $"{player.Name} hat sich verletzt und fällt voraussichtlich bis {until} aus.",
                    matchDate, humanTeam.Id, player.Id);
            }
        }

        // Public - CupMatchDayService/FriendlyMatchDayViewModel book career minutes for
        // cup/friendly matches using the same pattern.
        public static void ApplyCareerMinutes(MatchResult result, Team home, Team away)
        {
            foreach (var (playerId, minutes) in result.MinutesPlayed)
            {
                var player = home.Players.FirstOrDefault(p => p.Id == playerId)
                          ?? away.Players.FirstOrDefault(p => p.Id == playerId);
                if (player is null)
                    continue;

                player.CareerMinutesPlayed += minutes;
                player.SeasonMinutes += minutes;
                if (minutes > 0)
                    player.CareerAppearances++;
            }
        }

        // Rolls each match's per-game PlayerStats into that player's season-cumulative row
        // (PlayerId+Season) so leaderboards (top scorers, cards, ...) have data to read.
        private Task PersistPlayerStatsAsync(IReadOnlyList<MatchResult> results, int season) =>
            PersistPlayerStatsAsync(_players, results, season);

        // Reusable for competitions outside the league (cup matches have no
        // MatchDayService instance) - competition = null books as league stats (default
        // case, unchanged behavior for all existing callers).
        public static async Task PersistPlayerStatsAsync(
            PlayerRepository players, IReadOnlyList<MatchResult> results, int season, CompetitionType? competition = null)
        {
            foreach (var result in results)
            {
                foreach (var (playerId, matchStats) in result.PlayerMatchStats)
                {
                    var seasonStats = (await players.GetPlayerStatsAsync(playerId, season, competition)).FirstOrDefault()
                                       ?? new PlayerStats { PlayerId = playerId, Season = season, Competition = competition };
                    seasonStats.AddMatchStats(matchStats);
                    await players.SavePlayerStatsAsync(seasonStats);
                }
            }
        }

        private static void AdvanceDate(GameState state, IReadOnlyList<Fixture> fixtures, int matchday)
        {
            state.MatchdayIndex = matchday;
            var lastDate = fixtures.Count > 0 ? fixtures.Max(f => f.Date) : state.CurrentDate;
            state.CurrentDate = lastDate.AddDays(1);
        }
    }
}
