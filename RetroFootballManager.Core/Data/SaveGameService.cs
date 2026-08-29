using RetroFootballManager.Common;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;

namespace RetroFootballManager.Data
{
    // Bundles save-game logic: create a new game, load an existing save,
    // and persist progress at any time so play can continue.
    public class SaveGameService
    {
        private readonly AppDatabase _db;
        private readonly TeamRepository _teamRepository;
        private readonly GameStateRepository _gameStateRepository;
        private readonly LeagueRepository _leagueRepository;
        private readonly FixtureRepository _fixtureRepository;
        private readonly SponsorRepository _sponsorRepository;
        private readonly SponsorshipRepository _sponsorshipRepository;
        private readonly ContractRepository _contractRepository;
        private readonly CupTieRepository _cupTieRepository;
        private readonly TransferListingRepository _transferListingRepository;
        private readonly TrophyRepository _trophyRepository;
        private readonly ScoutingAssignmentRepository _scoutingRepository;
        private readonly ScoutingFocusRepository _scoutingFocusRepository;
        private readonly ScoutedPlayerRepository _scoutedPlayerRepository;
        private readonly PlayerRepository _playerRepository;
        private readonly MessageService _messageService;

        public SaveGameService(AppDatabase db)
        {
            _db = db;
            _teamRepository = new TeamRepository(db);
            _gameStateRepository = new GameStateRepository(db);
            _leagueRepository = new LeagueRepository(db);
            _fixtureRepository = new FixtureRepository(db);
            _sponsorRepository = new SponsorRepository(db);
            _sponsorshipRepository = new SponsorshipRepository(db);
            _contractRepository = new ContractRepository(db);
            _cupTieRepository = new CupTieRepository(db);
            _transferListingRepository = new TransferListingRepository(db);
            _trophyRepository = new TrophyRepository(db);
            _scoutingRepository = new ScoutingAssignmentRepository(db);
            _scoutingFocusRepository = new ScoutingFocusRepository(db);
            _scoutedPlayerRepository = new ScoutedPlayerRepository(db);
            _playerRepository = new PlayerRepository(db);
            _messageService = new MessageService(new MessageRepository(db));
        }

        public Task RecordTrophyWinAsync(int teamId, TrophyType type, int season) =>
            _trophyRepository.RecordWinAsync(teamId, type, season);

        public Task<List<TrophyRecord>> GetTrophiesForTeamAsync(int teamId) =>
            _trophyRepository.GetByTeamAsync(teamId);

        // Starts a 2-week scouting assignment - in addition to ScoutingService.TryStartScouting
        // (scout present? already scouted?), also checks that no assignment for this exact
        // player is already running. Async methods can't have "out", hence the tuple return.
        public async Task<(bool Success, string? Error)> TryStartScoutingAsync(Team team, Player player, DateTime currentDate)
        {
            if (!ScoutingService.TryStartScouting(team, player, out string? error))
                return (false, error);

            var existing = await _scoutingRepository.GetForPlayerAsync(team.Id, player.Id);
            if (existing is not null)
                return (false, "Dieser Spieler wird bereits gescoutet.");

            var activeAssignments = await _scoutingRepository.GetByTeamAsync(team.Id);
            var scout = ScoutingService.FindScoutWithCapacity(team, activeAssignments);
            if (scout is null)
                return (false, $"Alle Scouts sind derzeit ausgebucht ({ScoutingService.MaxConcurrentAssignmentsPerScout}/{ScoutingService.MaxConcurrentAssignmentsPerScout}) - sie müssen erst ihre aktuellen Aufgaben abschließen.");

            await _scoutingRepository.SaveAsync(ScoutingService.CreateAssignment(team.Id, player.Id, currentDate, scout.Id));
            return (true, null);
        }

        public Task<List<ScoutingAssignment>> GetActiveScoutingAsync(int teamId) =>
            _scoutingRepository.GetByTeamAsync(teamId);

        public Task<List<ScoutingFocus>> GetScoutingFocusesAsync(int teamId) =>
            _scoutingFocusRepository.GetByTeamAsync(teamId);

        // Assigns (or replaces, if the scout already has one) a ScoutingFocus - rejected while
        // the scout is already at capacity (see ScoutingService.TryAssignFocus).
        public async Task<(bool Success, string? Error)> TryAssignScoutingFocusAsync(
            Team team, Employee scout, ScoutingFocus newFocus, DateTime currentDate)
        {
            var activeAssignments = await _scoutingRepository.GetByTeamAsync(team.Id);
            if (!ScoutingService.TryAssignFocus(scout, activeAssignments, out string? error))
                return (false, error);

            var existing = await _scoutingFocusRepository.GetForScoutAsync(scout.Id);
            newFocus.Id = existing?.Id ?? 0;
            newFocus.TeamId = team.Id;
            newFocus.ScoutEmployeeId = scout.Id;
            newFocus.CreatedDate = currentDate;

            await _scoutingFocusRepository.SaveAsync(newFocus);
            return (true, null);
        }

        // Daily tick: for every ScoutingFocus with a still-employed scout, tops that scout up
        // to MaxConcurrentAssignmentsPerScout with the best-matching candidates, until capacity
        // is full or no more candidates remain. Called alongside ApplyDueScoutingAsync.
        public async Task ApplyScoutingFocusesAsync(Team team, IReadOnlyList<Team> allTeams, DateTime currentDate)
        {
            var foci = await _scoutingFocusRepository.GetByTeamAsync(team.Id);
            if (foci.Count == 0)
                return;

            var activeAssignments = await _scoutingRepository.GetByTeamAsync(team.Id);
            var rng = new Random(HashCode.Combine(team.Id, currentDate.Year, currentDate.Month, currentDate.Day));

            foreach (var focus in foci)
            {
                var scout = team.Employees.FirstOrDefault(e => e.Id == focus.ScoutEmployeeId && e.EmployeeType == EmployeeType.Scout);
                if (scout is null)
                    continue;

                int load = activeAssignments.Count(a => a.ScoutEmployeeId == scout.Id);
                int capacity = ScoutingService.MaxConcurrentAssignmentsPerScout - load;
                if (capacity <= 0)
                    continue;

                var candidates = ScoutingService.FindCandidatesForFocus(team, focus, allTeams, rng, take: capacity);
                foreach (var candidate in candidates)
                {
                    if (await _scoutingRepository.GetForPlayerAsync(team.Id, candidate.Id) is not null)
                        continue;

                    var assignment = ScoutingService.CreateAssignment(team.Id, candidate.Id, currentDate, scout.Id);
                    await _scoutingRepository.SaveAsync(assignment);
                    activeAssignments.Add(assignment);
                }
            }
        }

        public Task<ScoutingAssignment?> GetActiveScoutingForPlayerAsync(int teamId, int playerId) =>
            _scoutingRepository.GetForPlayerAsync(teamId, playerId);

        // Daily tick: completes due scouting assignments (Player.IsScouted = true, assignment
        // deleted, mailbox message) - the scouted player must be saved directly since they aren't
        // necessarily on the scouting (human) team's own roster.
        // Returns the IDs of newly scouted players so the caller (CalendarAdvanceService) can
        // keep the in-memory Player objects in GameSession.Teams in sync - GetPlayerAsync below
        // otherwise loads a separate, fresh object from the DB that never reconnects with the
        // object in GameSession.Teams (stale-IsScouted bug).
        public async Task<List<int>> ApplyDueScoutingAsync(int teamId, DateTime currentDate)
        {
            var scoutedPlayerIds = new List<int>();
            var assignments = await _scoutingRepository.GetByTeamAsync(teamId);
            foreach (var assignment in assignments.Where(a => a.CompletionDate <= currentDate))
            {
                var player = await _playerRepository.GetPlayerAsync(assignment.PlayerId);
                if (player is not null)
                {
                    player.IsScouted = true;
                    await _playerRepository.SavePlayerAsync(player);
                    await _messageService.SendAsync(
                        MessageType.ScoutingCompleted, "Scouting abgeschlossen",
                        $"{player.Name} wurde vollständig gescoutet - das Spielerprofil ist jetzt einsehbar.",
                        currentDate, relatedPlayerId: player.Id);
                    await _scoutedPlayerRepository.SaveAsync(new ScoutedPlayer
                    {
                        TeamId = teamId,
                        PlayerId = player.Id,
                        ScoutedDate = currentDate,
                    });
                    scoutedPlayerIds.Add(player.Id);
                }
                await _scoutingRepository.DeleteAsync(assignment.Id);
            }
            return scoutedPlayerIds;
        }

        public Task<List<ScoutedPlayer>> GetScoutedPlayersAsync(int teamId) =>
            _scoutedPlayerRepository.GetByTeamAsync(teamId);

        public Task RemoveScoutedPlayerAsync(int teamId, int playerId) =>
            _scoutedPlayerRepository.RemoveAsync(teamId, playerId);

        public async Task InitializeAsync() => await _db.InitializeAsync();

        public Task CloseAsync() => _db.CloseAsync();

        public Task<bool> HasSaveGameAsync() =>
            _gameStateRepository.GetAsync().ContinueWith(t => t.Result is not null);

        public Task DeleteSaveAsync() => _db.ClearGameDataAsync();

        public async Task<GameState> NewGameAsync(string saveName, int managerTeamId, IEnumerable<Team> teams)
        {
            foreach (var team in teams)
                await _teamRepository.SaveTeamAsync(team);

            var state = new GameState
            {
                SaveName = saveName,
                ManagerTeamId = managerTeamId,
                Season = 1,
                CurrentDate = new DateTime(2026, 8, 1),
                MatchdayIndex = 0,
                CreatedAt = DateTime.UtcNow,
                LastSavedAt = DateTime.UtcNow,
            };

            await _gameStateRepository.SaveAsync(state);
            return state;
        }

        // Sets up a complete new career game: saves all teams (the DB assigns the final team
        // IDs), saves the leagues, builds the fixture list (home/away) from the now-known IDs,
        // and creates the GameState.
        // managerTeam must be an object from teams (its Id is read after saving).
        public async Task<GameState> StartNewCareerAsync(
            string saveName,
            int season,
            IReadOnlyList<League> leagues,
            IReadOnlyList<Team> teams,
            Team managerTeam,
            DateTime seasonStart,
            Difficulty difficulty = Difficulty.Normal)
        {
            // Fully clear the old save - otherwise teams/leagues from previous "new game" runs
            // pile up (e.g. 36 instead of 18 teams per league).
            await _db.ClearGameDataAsync();

            // The manager knows the full abilities only of their own squad (incl. youth) -
            // every other club stays unscouted until a scout later observes them.
            foreach (var player in managerTeam.Players.Concat(managerTeam.YouthPlayers))
                player.IsScouted = true;

            // Marks the start month (preseason begin) as already "settled" for ALL teams, so the
            // first REAL monthly settlement (salaries/upkeep/sponsoring) only fires on the 15th of
            // the FOLLOWING month - otherwise a brand-new career would deduct salaries after just
            // ~2 weeks (the 15th of the start month), which feels like an instant punishment right
            // after starting. Deliberately only applies to the very first season start, not to
            // AdvanceToNextSeasonAsync (there teams already have a real settlement history).
            var initialDate = seasonStart.AddMonths(-2);
            foreach (var team in teams.Where(t => t.Finances is not null))
            {
                team.Finances!.LastSettlementMonth = initialDate.Month;
                team.Finances.LastSettlementYear = initialDate.Year;
            }

            foreach (var team in teams)
                await _teamRepository.SaveTeamAsync(team);

            foreach (var league in leagues)
                await _leagueRepository.SaveAsync(league);

            var firstSaturday = FixtureGenerator.FirstSaturdayOnOrAfter(seasonStart);
            var allFixtures = new List<Fixture>();
            foreach (var tier in teams.Select(t => t.LeagueTier).Distinct().OrderBy(t => t))
            {
                var teamIds = teams.Where(t => t.LeagueTier == tier).Select(t => t.Id).ToList();
                allFixtures.AddRange(
                    FixtureGenerator.GenerateLeagueFixtures(teamIds, season, tier, firstSaturday));
            }

            await _fixtureRepository.InsertAllAsync(allFixtures);

            await SeedEconomyAsync(teams, season, seasonStart, managerTeam.Id);
            await SeedGermanCupAsync(teams, season, seasonStart);
            await SeedContinentalCupsAsync(teams, season, seasonStart, previousSeason: null);

            var state = new GameState
            {
                SaveName = saveName,
                ManagerTeamId = managerTeam.Id,
                Season = season,
                // Preseason (M7): 2 months before matchday 1, same as AdvanceToNextSeasonAsync.
                CurrentDate = seasonStart.AddMonths(-2),
                SeasonStart = seasonStart,
                MatchdayIndex = 0,
                CreatedAt = DateTime.UtcNow,
                LastSavedAt = DateTime.UtcNow,
                Difficulty = difficulty,
            };

            await _gameStateRepository.SaveAsync(state);
            return state;
        }

        // Creates a fresh sponsor catalog (reference data, no progress) and gives every AI team
        // a matching deal per slot (main/perimeter/kit) plus a contract for the starting
        // assistant coach. Runs after saving teams so team/employee IDs are set.
        // The player-managed team deliberately gets NO automatic sponsor - otherwise all slots
        // would already be "negotiated" for 2-4 seasons at career start and no longer freely
        // choosable via the SponsorsPage (see historical bug).
        private async Task SeedEconomyAsync(IReadOnlyList<Team> teams, int season, DateTime seasonStart, int managerTeamId)
        {
            var catalog = SponsorCatalog.CreateDefaultCatalog();
            foreach (var sponsor in catalog)
                await _sponsorRepository.SaveAsync(sponsor);

            var rng = new Random();
            foreach (var team in teams)
            {
                if (team.Id != managerTeamId)
                {
                    foreach (var slot in new[] { SponsorType.Main, SponsorType.Perimeter, SponsorType.Kit })
                    {
                        if (slot == SponsorType.Kit && team.LeagueTier > 2)
                            continue;

                        var offers = catalog.Where(s => s.SponsorType == slot && team.LeagueTier <= s.MinTier).ToList();
                        if (offers.Count == 0)
                            continue;

                        var chosen = offers[rng.Next(offers.Count)];
                        await _sponsorshipRepository.SaveAsync(new Sponsorship
                        {
                            TeamId = team.Id,
                            SponsorId = chosen.Id,
                            SponsorType = slot,
                            StartSeason = season,
                            Duration = 2 + rng.Next(3),
                        });
                    }
                }

                var coach = team.Employees.FirstOrDefault(e => e.EmployeeType == EmployeeType.AssistantCoach);
                if (coach is not null)
                {
                    await _contractRepository.SaveAsync(new Contract
                    {
                        HolderId = coach.Id,
                        HolderType = ContractHolderType.Employee,
                        TeamId = team.Id,
                        StartDate = seasonStart,
                        EndDate = seasonStart.AddYears(3),
                        AnnualSalary = coach.Salary,
                        MarketValue = coach.MarketValue,
                    });
                }

                foreach (var contract in PlayerContractService.SeedInitialContracts(team, seasonStart))
                    await _contractRepository.SaveAsync(contract);
            }
        }

        // Draws the German Cup preliminary round (requires at least 64 teams - smaller test
        // scenarios deliberately go without a cup instead of risking an ArgumentException).
        private async Task SeedGermanCupAsync(IReadOnlyList<Team> teams, int season, DateTime seasonStart)
        {
            if (teams.Count < 64)
                return;

            var date = CupCalendarService.GetKoRoundDate(CompetitionType.GermanCup, CupDrawService.RoundPreliminary, seasonStart);
            var ties = CupDrawService.BuildGermanCupFirstRound(teams, season, date);
            await _cupTieRepository.InsertAllAsync(ties);
        }

        // Determines the German CL/Europa Cup qualifiers (league-1 positions 1-4 / 5-7).
        // Without a previous season (career start, previousSeason = null) there's no real
        // standings table yet - so just take the first 4/next 3 league-1 teams
        // (by Id, i.e. effectively random, as requested by the user).
        private async Task<(List<Team> ChampionsLeague, List<Team> EuropaCup)> DetermineContinentalQualifiersAsync(
            int? previousSeason, IReadOnlyList<Team> teams)
        {
            var teamById = teams.ToDictionary(t => t.Id);

            if (previousSeason is null)
            {
                var ordered = teams.Where(t => t.LeagueTier == 1).OrderBy(t => t.Id).ToList();
                return (ordered.Take(4).ToList(), ordered.Skip(4).Take(3).ToList());
            }

            var previousFixtures = await _fixtureRepository.GetByLeagueAsync(previousSeason.Value, leagueTier: 1);
            var names = teams.ToDictionary(t => t.Id, t => t.Name);
            var standings = StandingsCalculator.Calculate(previousFixtures, names)
                .OrderBy(s => s.Position)
                .ToList();

            return (
                standings.Take(4).Select(s => teamById[s.TeamId]).ToList(),
                standings.Skip(4).Take(3).Select(s => teamById[s.TeamId]).ToList());
        }

        // Draws Champions League + Europa Cup for the season: fictional clubs (M6c/M6d)
        // + German qualifiers -> pot draw -> group stage, fixed calendar dates.
        // Smaller test scenarios (few league-1 teams) deliberately go without CL/EL.
        private async Task SeedContinentalCupsAsync(
            IReadOnlyList<Team> teams, int season, DateTime seasonStart, int? previousSeason)
        {
            if (teams.Count(t => t.LeagueTier == 1) < 7)
                return;

            var (clQualifiers, elQualifiers) = await DetermineContinentalQualifiersAsync(previousSeason, teams);
            var rng = new Random();

            await SeedContinentalCupAsync(
                CompetitionType.ChampionsLeague, ForeignClubGenerator.Competition.ChampionsLeague,
                clQualifiers, season, seasonStart, rng);
            await SeedContinentalCupAsync(
                CompetitionType.EuropaCup, ForeignClubGenerator.Competition.EuropaCup,
                elQualifiers, season, seasonStart, rng);
        }

        private async Task SeedContinentalCupAsync(
            CompetitionType competitionType, ForeignClubGenerator.Competition foreignCompetition,
            IReadOnlyList<Team> germanQualifiers, int season, DateTime seasonStart, Random rng)
        {
            var foreignClubs = ForeignClubGenerator.GenerateClubs(foreignCompetition, rng);
            foreach (var club in foreignClubs)
                await _teamRepository.SaveTeamAsync(club, includeYouth: false);

            var participants = GroupDrawService.BuildParticipants(foreignClubs, germanQualifiers);
            var groups = GroupDrawService.DrawGroups(participants, rng);

            var firstMatchday = CupCalendarService.GetGroupMatchdayDate(competitionType, matchdayIndex: 0, seasonStart);
            var ties = GroupDrawService.BuildGroupStageFixtures(groups, competitionType, season, firstMatchday);
            await _cupTieRepository.InsertAllAsync(ties);
        }

        // Determines the team's next due match in ONE competition: automatically simulates every
        // complete round/group matchday the team isn't involved in (no spectator interest without
        // your own team), draws the next round/round of 16 once the current stage is fully
        // played. Returns null if the competition isn't running this season or the team is
        // eliminated/done.
        // The group stage (round 0) is split into matchdays via the date (12 matches per group
        // share 6 dates); the KO stage (from CupDrawService.RoundLastSixteen for CL/EL, from
        // RoundPreliminary for the cup) runs purely round by round as before.
        public async Task<CupTie?> GetNextCompetitionTieForTeamAsync(
            CompetitionType competition, int season, int teamId, IReadOnlyList<Team> teams,
            CupMatchDayService cupMatchDayService, DateTime seasonStart)
        {
            while (true)
            {
                var allTies = await _cupTieRepository.GetBySeasonAsync(season, competition);
                if (allTies.Count == 0)
                    return null;

                bool groupStagePending = allTies.Any(t => t.Round == 0 && !t.Played);
                int currentRound = groupStagePending ? 0 : allTies.Max(t => t.Round);
                var roundTies = allTies.Where(t => t.Round == currentRound).ToList();

                // "Slot" is the earliest still-open date within the round - covers group
                // matchdays (multiple dates per round 0) and home/away leg dates (2 dates per KO
                // round) uniformly, without a special case.
                var pendingDates = roundTies.Where(t => !t.Played).Select(t => t.Date).ToList();
                var slotTies = pendingDates.Count == 0
                    ? roundTies
                    : roundTies.Where(t => t.Date == pendingDates.Min()).ToList();

                // Only return early when the team still has something left to play - once their
                // own tie is done, fall through so the round-completion/next-round-draw logic
                // below still runs for the rest of the bracket. Otherwise a team eliminated
                // mid-competition (as opposed to one that never qualified at all, which never
                // matches here) would permanently stall every other team's remaining rounds and
                // prize money, since this per-team method is the only place round advancement
                // happens.
                var humanTie = slotTies.FirstOrDefault(t => t.HomeTeamId == teamId || t.AwayTeamId == teamId);
                if (humanTie is not null && !humanTie.Played)
                    return humanTie;

                if (!slotTies.All(t => t.Played))
                {
                    // PlayCupRoundAsync blindly (re-)simulates every tie it's given - exclude
                    // ones already played (e.g. the queried team's own result, resolved earlier
                    // via the branch above) so they aren't silently overwritten.
                    var pendingSlotTies = slotTies.Where(t => !t.Played).ToList();
                    var firstLegTies = roundTies.Where(t => t.LegNumber == CupTie.LegFirst).ToList();
                    await cupMatchDayService.PlayCupRoundAsync(
                        teams, pendingSlotTies, humanTie: null, humanResult: null, humanTeamId: teamId, firstLegTies: firstLegTies);
                    continue;
                }

                if (!roundTies.All(t => t.Played))
                    continue; // next matchday/return-leg slot will be found on the next pass

                bool groupStageJustFinished = roundTies[0].Round == 0;
                if (groupStageJustFinished)
                {
                    var koDate = CupCalendarService.GetKoRoundDate(competition, CupDrawService.RoundLastSixteen, seasonStart);
                    var koSecondLegDate = CupCalendarService.GetKoRoundDate(
                        competition, CupDrawService.RoundLastSixteen, seasonStart, secondLeg: true);
                    var names = teams.ToDictionary(t => t.Id, t => t.Name);
                    var groupTables = allTies.Where(t => t.Round == 0)
                        .GroupBy(t => t.Group!)
                        .ToDictionary(g => g.Key, g => GroupDrawService.CalculateGroupTable(g.ToList(), names));

                    var roundOfSixteen = GroupDrawService.BuildRoundOfSixteen(groupTables, competition, season, koDate, koSecondLegDate);
                    await _cupTieRepository.InsertAllAsync(roundOfSixteen);
                    continue;
                }

                int round = roundTies[0].Round;
                if (round == CupDrawService.RoundFinal)
                    return null;

                var nextDate = CupCalendarService.GetKoRoundDate(competition, round + 1, seasonStart);
                bool nextTwoLegged = CupTieHelper.IsTwoLegged(competition, round + 1);
                DateTime? secondLegDate = nextTwoLegged
                    ? CupCalendarService.GetKoRoundDate(competition, round + 1, seasonStart, secondLeg: true)
                    : null;

                var teamsById = teams.ToDictionary(t => t.Id);
                var nextRound = CupDrawService.BuildNextRound(roundTies, teamsById, season, nextDate, random: null, secondLegDate: secondLegDate);

                await _cupTieRepository.InsertAllAsync(nextRound);
            }
        }

        // Cheap upfront check whether the team even takes part in this competition - avoids
        // GetNextCompetitionTieForTeamAsync accidentally simulating an entire (for the player
        // irrelevant) competition right away for a non-participating team.
        public async Task<bool> IsTeamInCompetitionAsync(CompetitionType competition, int season, int teamId)
        {
            var ties = await _cupTieRepository.GetBySeasonAsync(season, competition);
            return ties.Any(t => t.HomeTeamId == teamId || t.AwayTeamId == teamId);
        }

        public Task SaveCupTieAsync(CupTie tie) => _cupTieRepository.SaveAsync(tie);

        // "Other matches in the same slot" as tie - all matches of the same round AND the same
        // date (covers group matchdays as well as the home/away leg date of a KO round -
        // both have multiple dates per round).
        public async Task<List<CupTie>> GetSameSlotTiesAsync(CompetitionType competition, int season, CupTie tie)
        {
            var allTies = await _cupTieRepository.GetBySeasonAsync(season, competition);
            return allTies.Where(t => t.Round == tie.Round && t.Date == tie.Date).ToList();
        }

        public Task<List<CupTie>> GetCupTiesAsync(int season, CompetitionType competition) =>
            _cupTieRepository.GetBySeasonAsync(season, competition);

        // For display/penalty-shootout decision: the first leg for a given second-leg match.
        public async Task<CupTie?> GetFirstLegAsync(CompetitionType competition, int season, CupTie secondLeg)
        {
            if (secondLeg.LegNumber != CupTie.LegSecond)
                return null;

            var roundTies = await _cupTieRepository.GetByRoundAsync(season, competition, secondLeg.Round);
            return roundTies.FirstOrDefault(t => t.MatchNumberInRound == secondLeg.MatchNumberInRound && t.LegNumber == CupTie.LegFirst);
        }

        // Persists only the GameState (e.g. after MatchDayService has already saved
        // teams/fixtures and advanced the date).
        public Task SaveStateAsync(GameState state)
        {
            state.LastSavedAt = DateTime.UtcNow;
            return _gameStateRepository.SaveAsync(state);
        }

        public Task<List<League>> GetLeaguesAsync(int season) =>
            _leagueRepository.GetBySeasonAsync(season);

        public Task SaveFixtureAsync(Fixture fixture) => _fixtureRepository.SaveAsync(fixture);

        public async Task<PlayerStats?> GetPlayerSeasonStatsAsync(int playerId, int season) =>
            (await _playerRepository.GetPlayerStatsAsync(playerId, season)).FirstOrDefault();

        // All-time LEAGUE totals across every season for this player - cup and friendly stats
        // are tracked separately (see GetPlayerCompetitionBreakdownAsync) and no longer blended
        // in here.
        public async Task<PlayerStats> GetPlayerCareerStatsAsync(int playerId)
        {
            var career = new PlayerStats { PlayerId = playerId };
            foreach (var row in await _playerRepository.GetAllStatsByCompetitionAsync(playerId, competition: null))
                career.AddMatchStats(row);
            return career;
        }

        private static readonly (string Label, CompetitionType Competition)[] NonLeagueCompetitions =
        [
            ("Freundschaftsspiele", CompetitionType.Friendly),
            ("Deutscher Pokal", CompetitionType.GermanCup),
            ("Champions League", CompetitionType.ChampionsLeague),
            ("Europa Cup", CompetitionType.EuropaCup),
        ];

        // Career totals for every non-league competition (friendlies + each of the 3 cups),
        // kept separate from the league-only GetPlayerCareerStatsAsync above.
        public async Task<List<CompetitionStatsRow>> GetPlayerCompetitionBreakdownAsync(int playerId)
        {
            var rows = new List<CompetitionStatsRow>();
            foreach (var (label, competition) in NonLeagueCompetitions)
            {
                var agg = new PlayerStats { PlayerId = playerId };
                foreach (var row in await _playerRepository.GetAllStatsByCompetitionAsync(playerId, competition))
                    agg.AddMatchStats(row);
                rows.Add(new CompetitionStatsRow(label, agg.Appearances, agg.Goals, agg.Assists, agg.YellowCards, agg.RedCards));
            }
            return rows;
        }

        public Task<List<Fixture>> GetFixturesAsync(int season) =>
            _fixtureRepository.GetBySeasonAsync(season);

        // Reloads ALL teams from the DB (incl. fictional CL/Europa Cup clubs that are never
        // part of the original 72-team list) - needed so GameSession.Teams is complete for cup
        // simulation after StartNewCareerAsync/AdvanceToNextSeasonAsync.
        public Task<List<Team>> GetAllTeamsAsync() => _teamRepository.GetAllTeamsAsync();

        // For profile display/renewal: a player's currently active contract (null if none
        // exists - e.g. fictional/unscouted squads without seeding).
        public async Task<Contract?> GetActivePlayerContractAsync(int playerId, DateTime asOf)
        {
            var contracts = await _contractRepository.GetByHolderAsync(playerId, ContractHolderType.Player);
            return PlayerContractService.GetActiveContract(playerId, contracts, asOf);
        }

        public Task SaveContractAsync(Contract contract) => _contractRepository.SaveAsync(contract);

        // For profile display: whether and how a player is currently offered on the transfer
        // market (transfer or loan) - null if not listed.
        public Task<TransferListing?> GetTransferListingForPlayerAsync(int playerId) =>
            _transferListingRepository.GetByPlayerAsync(playerId);

        // Evaluates the season end (standings, promotion/relegation, career points) and rolls
        // into the next season: reset teams, build new fixtures/leagues, advance GameState
        // and save everything.
        public async Task<SeasonEndResult> AdvanceToNextSeasonAsync(
            GameState state, List<Team> teams, CareerService career)
        {
            var fixtures = await _fixtureRepository.GetBySeasonAsync(state.Season);
            var result = SeasonProgressionService.EndSeason(
                state.Season, teams, fixtures, state.ManagerTeamId, career);

            if (result.ManagerFinalPosition == 1)
                await RecordTrophyWinAsync(state.ManagerTeamId, TrophyMapping.FromLeagueTier(result.ManagerTier), state.Season);

            // Club mood: season-end structural events (champion/promotion/relegation) for the
            // human team - see ClubMoodService.
            var managerTeam = teams.FirstOrDefault(t => t.Id == state.ManagerTeamId);
            if (managerTeam is not null)
            {
                if (result.ManagerFinalPosition == 1)
                    ClubMoodService.ApplyChampionship(managerTeam);
                if (result.ManagerPromoted)
                    ClubMoodService.ApplyPromotion(managerTeam);
                if (result.ManagerRelegated)
                    ClubMoodService.ApplyRelegation(managerTeam);
            }

            await PaySponsorSeasonBonusesAsync(teams, result);

            PrizeMoneyService.AwardLeaguePrizes(teams, result);
            foreach (var competition in new[] { CompetitionType.GermanCup, CompetitionType.ChampionsLeague, CompetitionType.EuropaCup })
            {
                var ties = await _cupTieRepository.GetBySeasonAsync(state.Season, competition);
                PrizeMoneyService.AwardCupPrizes(teams, ties, competition);
            }

            int newSeason = state.Season + 1;
            var seasonStart = NextSeasonStart(state.CurrentDate);

            // Age and develop players (youth matures/gets promoted), then reset teams
            // for the new season (stats row keeps its Id).
            var devRandom = new Random();
            foreach (var team in teams)
            {
                DevelopmentService.DevelopSquad(team, seasonStart, devRandom);
                int statsId = team.Statistics?.Id ?? 0;
                team.Statistics = new TeamStats { Id = statsId, TeamId = team.Id, Season = newSeason };
                MatchDayService.PrepareForMatch(team, seasonStart);
            }

            var newLeagues = Enumerable.Range(1, 4)
                .Select(tier => new League { Name = $"Liga {tier}", Tier = tier, Season = newSeason })
                .ToList();

            var newFixtures = SeasonProgressionService.BuildNextSeasonFixtures(teams, newSeason, seasonStart);

            foreach (var team in teams)
                await _teamRepository.SaveTeamAsync(team);
            foreach (var league in newLeagues)
                await _leagueRepository.SaveAsync(league);
            await _fixtureRepository.InsertAllAsync(newFixtures);

            await SeedGermanCupAsync(teams, newSeason, seasonStart);
            await SeedContinentalCupsAsync(teams, newSeason, seasonStart, previousSeason: state.Season);

            state.Season = newSeason;
            // Preseason (M7): 2 months before matchday 1, instead of jumping straight in -
            // training camps/friendlies/transfers can happen during this time.
            // SeasonStart remains the matchday-1 anchor (CupCalendarService week offsets unchanged).
            state.CurrentDate = seasonStart.AddMonths(-2);
            state.SeasonStart = seasonStart;
            state.MatchdayIndex = 0;
            state.LastSavedAt = DateTime.UtcNow;
            await _gameStateRepository.SaveAsync(state);

            return result;
        }

        // Pays all sponsor bonuses in one lump sum at season end - win bonus (per win actually
        // achieved this season), promotion bonus, and the placement bonuses (champion/top 5/
        // midfield). None of these are paid during the season itself; BonusForTop5,
        // BonusForMidfieldPlace and BonusForMasterTitle were previously dead fields, never
        // paid out at all. Public (instead of private) for direct testability, analogous to
        // CupMatchDayService.ResolvePenaltyShootout.
        public async Task PaySponsorSeasonBonusesAsync(IReadOnlyList<Team> teams, SeasonEndResult result)
        {
            var teamsById = teams.ToDictionary(t => t.Id);
            var catalog = await _sponsorRepository.GetAllAsync();

            foreach (var league in result.Leagues)
            {
                foreach (var row in league.Table)
                {
                    if (!teamsById.TryGetValue(row.TeamId, out var team) || team.Finances is null)
                        continue;

                    bool promoted = league.PromotedTeamIds.Contains(row.TeamId);
                    bool relegated = league.RelegatedTeamIds.Contains(row.TeamId);

                    var deals = await _sponsorshipRepository.GetByTeamAsync(row.TeamId);
                    foreach (var deal in deals)
                    {
                        var sponsor = catalog.FirstOrDefault(s => s.Id == deal.SponsorId);
                        if (sponsor is null)
                            continue;

                        int bonus = sponsor.BonusPerWin * row.Wins;

                        if (row.Position == 1)
                            bonus += sponsor.BonusForMasterTitle;
                        else if (row.Position <= 5)
                            bonus += sponsor.BonusForTop5;
                        else if (!relegated)
                            bonus += sponsor.BonusForMidfieldPlace;

                        if (promoted)
                            bonus += sponsor.BonusPerPromotion;

                        if (bonus <= 0)
                            continue;

                        team.Finances.CurrentBalance += bonus;
                        team.Finances.SponsorIncome += bonus;
                    }
                }
            }
        }

        // Next season starts on August 1st after the current game date.
        private static DateTime NextSeasonStart(DateTime currentDate)
        {
            var candidate = new DateTime(currentDate.Year, 8, 1);
            return candidate <= currentDate ? candidate.AddYears(1) : candidate;
        }

        public async Task<(GameState State, List<Team> Teams)?> LoadGameAsync()
        {
            var state = await _gameStateRepository.GetAsync();
            if (state is null)
                return null;

            var teams = await _teamRepository.GetAllTeamsAsync();

            // Legacy safety net for saves created before goalkeeper-specific / Finishing+Positioning
            // attributes existed (see PlayerGenerator.BackfillGoalkeeperAttributesIfMissing /
            // BackfillFinishingAndPositioningIfMissing) - fixed in memory here, persisted the next
            // time the team is saved.
            foreach (var team in teams)
            {
                foreach (var p in team.Players)
                {
                    PlayerGenerator.BackfillGoalkeeperAttributesIfMissing(p);
                    PlayerGenerator.BackfillFinishingAndPositioningIfMissing(p);
                    PlayerGenerator.BackfillBaseFitnessIfMissing(p);
                    PlayerGenerator.BackfillInMatchCharacterIfMissing(p);
                }
                foreach (var p in team.YouthPlayers)
                {
                    PlayerGenerator.BackfillGoalkeeperAttributesIfMissing(p);
                    PlayerGenerator.BackfillFinishingAndPositioningIfMissing(p);
                    PlayerGenerator.BackfillBaseFitnessIfMissing(p);
                    PlayerGenerator.BackfillInMatchCharacterIfMissing(p);
                }

                // Legacy safety net for saves created before portrait images existed - a no-op
                // once ImagePath is set or the player/employee is too old for the (young-faces-
                // only) pack, safe to call on every load.
                FaceImageAssigner.AssignPlayerFaces(team.Players.Concat(team.YouthPlayers), Random.Shared);
                foreach (var e in team.Employees)
                {
                    StaffGenerator.BackfillAgeIfMissing(e);
                    StaffGenerator.FixGenderNameMismatch(e);
                }
                FaceImageAssigner.AssignStaffFaces(team.Employees, Random.Shared);

                // Legacy safety net for saves created before club mood existed: both fields
                // read 0 after the column is added - 0/0 simultaneously is not a realistic
                // organic state, so treat it as "never initialized" and reset to the neutral
                // starting value (matches Team.cs's property-initializer default for new teams).
                if (team.FanMood == 0 && team.BoardMood == 0)
                {
                    team.FanMood = 65;
                    team.BoardMood = 65;
                }
            }

            return (state, teams);
        }

        public async Task SaveProgressAsync(GameState state, IEnumerable<Team> teams)
        {
            foreach (var team in teams)
                await _teamRepository.SaveTeamAsync(team);

            state.LastSavedAt = DateTime.UtcNow;
            await _gameStateRepository.SaveAsync(state);
        }

        // Persists a SINGLE team (e.g. after the manager tweaks lineup/training/youth) instead
        // of the whole league. SaveProgressAsync above re-saves every team in the game (all 72
        // in a full universe) - correct for season-wide state but far too slow for a screen
        // that only ever touches the manager's own team.
        public async Task SaveTeamProgressAsync(GameState state, Team team)
        {
            await _teamRepository.SaveTeamAsync(team);

            state.LastSavedAt = DateTime.UtcNow;
            await _gameStateRepository.SaveAsync(state);
        }

        // Persists just GameState.CurrentDate (+ the rest of GameState) - called once per day
        // from CalendarAdvanceService.AdvanceOneDayAsync so the calendar date can never end up
        // behind entity-level side effects (contracts, scouting assignments, ...) that already
        // write straight to the DB during the same tick. Without this, force-quitting mid
        // "Zeit vorstellen" (no explicit "Speichern") leaves CurrentDate reverted on reload
        // while those side effects stay - e.g. a scouting assignment whose StartDate is now in
        // the "future" relative to the reloaded CurrentDate.
        public Task SaveGameStateAsync(GameState state)
        {
            state.LastSavedAt = DateTime.UtcNow;
            return _gameStateRepository.SaveAsync(state);
        }
    }
}
