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
        //
        // A red-card ban is served per match played, not per calendar day, and only counts
        // in the competition it was picked up in - so this needs to know which match (if any)
        // is actually being prepared for:
        //   - isMatchDay = false (the daily calendar tick, no specific fixture): leave the ban
        //     untouched, just keep Status visible as Suspended.
        //   - isFriendly, or matchCompetition doesn't match the player's ban: not blocked here,
        //     available for this match, ban is left running for its own competition.
        //   - otherwise: this match serves one game of the ban.
        // Fitness regen after playing always takes at least this many days, no matter how high
        // BaseFitness (Grundfitness) is - see RegenerateFitness.
        public const int MinFitnessRecoveryDays = 3;
        // Days needed for full recovery at the lowest possible BaseFitness (1).
        private const int MaxFitnessRecoveryDays = 7;
        // Worst-case post-match fitness floor enforced by Match.DecayFitness.
        private const int PostMatchFitnessFloor = 20;

        public static void RecoverForMatch(
            Team team, DateTime? currentDate = null, CompetitionType? matchCompetition = null,
            bool isFriendly = false, bool isMatchDay = true)
        {
            double physioSpeedupFactor = ComputePhysioSpeedupFactor(team);

            foreach (var p in team.Players)
            {
                // Fitness is only regenerated on the daily calendar tick (isMatchDay: false) -
                // pre-match prep (isMatchDay: true) leaves it exactly as the last daily tick set
                // it, so playing again shortly after a match finds the squad still tired (see
                // RegenerateFitness). Without a currentDate (tests, no calendar context) fall
                // back to the old instant-heal behaviour.
                if (!isMatchDay)
                {
                    if (currentDate.HasValue)
                        RegenerateFitness(p, currentDate.Value, physioSpeedupFactor);
                    else
                        p.Fitness = 100;
                }

                if (p.Status == PlayerStatus.Injured)
                {
                    bool stillOut = currentDate.HasValue && p.InjuredUntil.HasValue && p.InjuredUntil.Value > currentDate.Value;
                    if (stillOut)
                        continue;
                    p.Status = PlayerStatus.Available;
                    p.InjuredUntil = null;
                }
                else if (p.Status == PlayerStatus.SubstitutedOff)
                {
                    p.Status = PlayerStatus.OnBench;
                }

                if (p.SuspensionMatchesRemaining <= 0)
                {
                    if (p.Status == PlayerStatus.Suspended)
                        p.Status = PlayerStatus.Available;
                    continue;
                }

                if (!isMatchDay)
                {
                    p.Status = PlayerStatus.Suspended;
                    continue;
                }

                if (isFriendly || p.SuspensionCompetition != matchCompetition)
                {
                    p.Status = PlayerStatus.Available;
                    continue;
                }

                p.SuspensionMatchesRemaining--;
                p.Status = p.SuspensionMatchesRemaining > 0 ? PlayerStatus.Suspended : PlayerStatus.Available;
                if (p.SuspensionMatchesRemaining <= 0)
                    p.SuspensionCompetition = null;
            }
        }

        // Gradual, BaseFitness-dependent fitness regen for one calendar day. Called once per
        // day per team from CalendarAdvanceService.AdvanceOneDayAsync via RecoverForMatch
        // (isMatchDay: false) - never from pre-match prep, so a day's regen is never applied
        // twice. Recovery to 100 always takes at least MinFitnessRecoveryDays, even at the
        // highest possible BaseFitness; the lowest possible BaseFitness takes
        // MaxFitnessRecoveryDays.
        private static void RegenerateFitness(Player p, DateTime currentDate, double physioSpeedupFactor = 1.0)
        {
            if (p.Fitness >= 100)
                return;

            if (p.LastMatchDate is not DateTime last)
            {
                // No tracked appearance yet (new/legacy player) - just top up gently.
                p.Fitness = Math.Min(100, p.Fitness + 10);
                return;
            }

            int daysSince = (currentDate.Date - last.Date).Days;
            if (daysSince < 0)
                return;

            double staminaFactor = Math.Clamp(p.BaseFitness, 1, 99) / 99.0;
            int recoveryDays = Math.Max(MinFitnessRecoveryDays,
                (int)Math.Round((MaxFitnessRecoveryDays - ((MaxFitnessRecoveryDays - MinFitnessRecoveryDays) * staminaFactor)) * physioSpeedupFactor));

            if (daysSince >= recoveryDays)
            {
                p.Fitness = 100;
                return;
            }

            int regenPerDay = (int)Math.Ceiling((100.0 - PostMatchFitnessFloor) / recoveryDays);
            int cap = daysSince < MinFitnessRecoveryDays ? 99 : 100;
            p.Fitness = Math.Min(cap, p.Fitness + regenPerDay);
        }

        // Physiotherapist+MedicalStaff quality shortens the daily fitness-regen curve - the
        // whole pool stacks (sum of headcount), unlike the best-of-type pattern elsewhere.
        private static double ComputePhysioSpeedupFactor(Team team)
        {
            var staff = team.Employees
                .Where(e => e.EmployeeType is EmployeeType.Physiotherapist or EmployeeType.MedicalStaff)
                .ToList();
            if (staff.Count == 0)
                return 1.0;

            double avg = staff.Average(e => e.FitnessTraining);
            double baseFactor = avg >= 75 ? 0.8 : avg >= 60 ? 0.9 : 1.0;
            double stackBonus = Math.Min(0.15, (staff.Count - 1) * 0.05);
            return Math.Clamp(baseFactor - stackBonus, 0.6, 1.0);
        }

        // Small, weekly-recomputed morale bonus from having Physiotherapist/MedicalStaff on
        // staff - see TeamStats.PhysioMoraleBoost's doc comment for why it overwrites instead
        // of accumulating.
        public static void ApplyPhysioMoraleBoost(Team team)
        {
            if (team.Statistics is null)
                return;

            var staff = team.Employees
                .Where(e => e.EmployeeType is EmployeeType.Physiotherapist or EmployeeType.MedicalStaff)
                .ToList();
            if (staff.Count == 0)
            {
                team.Statistics.PhysioMoraleBoost = 0;
                return;
            }

            double avg = staff.Average(e => e.FitnessTraining);
            int perStaffBoost = avg >= 75 ? 2 : avg >= 60 ? 1 : 0;
            team.Statistics.PhysioMoraleBoost = Math.Min(6, perStaffBoost * staff.Count);
        }

        // Same pattern as ApplyPhysioMoraleBoost, for Psychologist/Motivation - stacks across
        // multiple hires.
        public static void ApplyPsychologistMoraleBoost(Team team)
        {
            if (team.Statistics is null)
                return;

            var staff = team.Employees.Where(e => e.EmployeeType == EmployeeType.Psychologist).ToList();
            if (staff.Count == 0)
            {
                team.Statistics.PsychologistMoraleBoost = 0;
                return;
            }

            double avg = staff.Average(e => e.Motivation);
            int perStaffBoost = avg >= 75 ? 2 : avg >= 60 ? 1 : 0;
            team.Statistics.PsychologistMoraleBoost = Math.Min(6, perStaffBoost * staff.Count);
        }

        // Recovers the team and then auto-picks a position-correct XI (used for COM teams).
        public static void PrepareForMatch(
            Team team, DateTime? currentDate = null, CompetitionType? matchCompetition = null, bool isFriendly = false)
        {
            RecoverForMatch(team, currentDate, matchCompetition, isFriendly);
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

            // Negotiating is always allowed regardless of this - only completing a transfer/loan
            // is gated on it (see TransferAiService.EvaluateIncomingOffersAsync). Uses the
            // matchday just played, not state.MatchdayIndex (not updated to it until AdvanceDate
            // below runs).
            var seasonFixturesForWindow = await _fixtures.GetBySeasonAsync(state.Season);
            bool isTransferWindowOpen = SeasonPhaseCalculator.IsTransferWindowOpen(state.CurrentDate, matchday, seasonFixturesForWindow);

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

                    var managerTeam = home.Id == state.ManagerTeamId ? home : away;

                    // Undo any in-match subs/red-card reshuffles: they apply only to this one
                    // match, never to the next matchday's default lineup (see
                    // LineupSelector.RestoreBaseline). No-ops if no baseline was ever confirmed
                    // (e.g. very first match) - the fallback below still fixes up anyone left
                    // stuck as SubstitutedOff, which RebuildViews (Lineup page) doesn't render as
                    // Bench or Reserve, so they'd otherwise simply vanish until the next
                    // RecoverForMatch.
                    LineupSelector.RestoreBaseline(managerTeam);
                    foreach (var p in managerTeam.Players.Where(p => p.Status == PlayerStatus.SubstitutedOff))
                        p.Status = PlayerStatus.OnBench;

                    bool humanIsHome = home.Id == state.ManagerTeamId;
                    int humanGoals = humanIsHome ? humanResult.HomeGoals : humanResult.AwayGoals;
                    int opponentGoals = humanIsHome ? humanResult.AwayGoals : humanResult.HomeGoals;
                    if (humanGoals > opponentGoals)
                        ClubMoodService.ApplyLeagueWin(managerTeam);
                    else
                        ClubMoodService.ApplyLeagueLossOrDraw(managerTeam);

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
                ConversationService.ApplyWeeklyDecay(team);
                ApplyPhysioMoraleBoost(team);
                ApplyPsychologistMoraleBoost(team);

                if (!isHuman && _aiManager is not null)
                    await _aiManager.RunWeeklyTickAsync(
                        team, state.Season, state.CurrentDate, state.Difficulty, _random, teamById, state.ManagerTeamId, matchday,
                        isTransferWindowOpen);
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
                int opponentTeamId = isHome ? fixture.AwayTeamId : fixture.HomeTeamId;
                int opponentTierRank = teamById[opponentTeamId].LeagueTier;

                var standings = await GetStandingsAsync(team.LeagueTier);
                _finance!.ApplyMatchdayFinance(team, isHome, standings, opponentTierRank);
                bool isHumanTeam = id == state.ManagerTeamId;
                await _finance.ApplyMonthlySettlementAsync(team, state.CurrentDate, sendMessage: isHumanTeam);

                if (isHumanTeam)
                {
                    await _finance.CheckFinanceWarningAsync(team, state.CurrentDate);
                    await _finance.CheckSeasonEndProjectionAsync(team, state, state.CurrentDate);
                }
            }
        }

        private static void ApplyResult(Fixture fixture, MatchResult result, Team home, Team away)
        {
            fixture.HomeGoals = result.HomeGoals;
            fixture.AwayGoals = result.AwayGoals;
            fixture.Played = true;

            result.ApplyToTeamStats(home.Statistics!, away.Statistics!);
            result.ApplyInjuryDurations(fixture.Date);
            result.ApplySuspensions(competition: null);
            ApplyCareerMinutes(result, home, away, fixture.Date);
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
        // cup/friendly matches using the same pattern. Also stamps LastMatchDate for anyone
        // who actually played (minutes > 0), the basis for RegenerateFitness's day-by-day
        // recovery curve - covers league, cup and friendly matches since they all funnel
        // through this one method.
        //
        // countsTowardCareerStats: false for friendlies - they still count for fitness/fatigue
        // (LastMatchDate/SeasonMinutes) but must not inflate the player's displayed
        // CareerMinutesPlayed/CareerAppearances (see PersistPlayerStatsAsync with
        // CompetitionType.Friendly for the separate friendly stats these numbers now live in).
        public static void ApplyCareerMinutes(
            MatchResult result, Team home, Team away, DateTime matchDate, bool countsTowardCareerStats = true)
        {
            ManagerGrowthService.ApplyMatchGrowth(home.ManagerProfile, result, isHome: true);
            ManagerGrowthService.ApplyMatchGrowth(away.ManagerProfile, result, isHome: false);

            foreach (var (playerId, minutes) in result.MinutesPlayed)
            {
                var player = home.Players.FirstOrDefault(p => p.Id == playerId)
                          ?? away.Players.FirstOrDefault(p => p.Id == playerId);
                if (player is null)
                    continue;

                if (countsTowardCareerStats)
                    player.CareerMinutesPlayed += minutes;
                player.SeasonMinutes += minutes;
                if (minutes > 0)
                {
                    if (countsTowardCareerStats)
                        player.CareerAppearances++;
                    player.LastMatchDate = matchDate;
                }
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
