using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Logging;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public class CalendarAdvanceService
    {
        private static readonly ILog Log = LogManager.GetLogger<CalendarAdvanceService>();

        private readonly TeamRepository _teams;
        private readonly FixtureRepository _fixtures;
        private readonly AiManagerService _aiManager;
        private readonly ExpiryWarningService _expiryWarnings;
        private readonly FinanceService _finance;
        private readonly TrainingCampService _trainingCamps;
        private readonly MessageService _messages;
        private readonly SaveGameService? _saveGame;
        private readonly Random _random;

        public CalendarAdvanceService(
            TeamRepository teams, FixtureRepository fixtures, AiManagerService aiManager,
            ExpiryWarningService expiryWarnings, FinanceService finance, TrainingCampService trainingCamps,
            MessageService messages, Random? random = null, SaveGameService? saveGame = null)
        {
            _teams = teams;
            _fixtures = fixtures;
            _aiManager = aiManager;
            _expiryWarnings = expiryWarnings;
            _finance = finance;
            _trainingCamps = trainingCamps;
            _messages = messages;
            _saveGame = saveGame;
            _random = random ?? Random.Shared;
        }

        public async Task AdvanceOneDayAsync(GameState state, IReadOnlyList<Team> teams)
        {
            state.CurrentDate = state.CurrentDate.AddDays(1);
            var teamsById = teams.ToDictionary(t => t.Id);
            var humanTeam = teamsById.GetValueOrDefault(state.ManagerTeamId);
            var touchedTeamIds = new HashSet<int>();

            if (humanTeam is not null)
                await MatchDayService.NotifyInjuryRecoveriesAsync(_messages, humanTeam, state.CurrentDate);

            foreach (var team in teams)
            {
                MatchDayService.RecoverForMatch(team, state.CurrentDate, isMatchDay: false);
                if (DevelopmentService.ApplyMonthlyDevelopment(team, state.CurrentDate, _random))
                    touchedTeamIds.Add(team.Id);
            }

            await _aiManager.ReturnExpiredLoansAsync(state.CurrentDate, teamsById);

            var seasonFixtures = await _fixtures.GetBySeasonAsync(state.Season);
            var phase = SeasonPhaseCalculator.Calculate(state.CurrentDate, state.MatchdayIndex, seasonFixtures);
            var windowEnd = TrainingCampService.GetWindowEndDate(state, phase, seasonFixtures);

            bool runWeeklyTick = (state.CurrentDate.Date - state.SeasonStart.Date).Days % 7 == 0;

            foreach (var team in teams.Where(t => t.Id != state.ManagerTeamId))
            {
                // One AI team's tick must never be able to take down the whole day's processing -
                // a single bad edge case (stale listing reference, unusual squad state, etc.) would
                // otherwise silently abort every remaining team AND propagate out of
                // AdvanceOneDayAsync entirely, killing a multi-day "Zeit vorstellen" run with no
                // visible feedback and no chance for a due match/message on a later day to ever
                // be reached.
                try
                {
                    if (runWeeklyTick)
                    {
                        await _aiManager.RunWeeklyTickAsync(
                            team, state.Season, state.CurrentDate, state.Difficulty, _random, teamsById, state.ManagerTeamId);
                        touchedTeamIds.Add(team.Id);
                    }

                    bool booked = await TrainingCampAiService.TryBookCampAsync(
                        team, state.CurrentDate, windowEnd, state.Difficulty, _trainingCamps, _random);
                    bool applied = await _trainingCamps.ApplyDueCampsAsync(team, state.CurrentDate, sendMessage: false);
                    bool settled = await _finance.ApplyMonthlySettlementAsync(team, state.CurrentDate, sendMessage: false);
                    if (booked || applied || settled)
                        touchedTeamIds.Add(team.Id);
                }
                catch (Exception ex)
                {
                    Log.Error($"AI tick for team {team.Id} ({team.Name}) on {state.CurrentDate:yyyy-MM-dd} failed - skipped.", ex);
                }
            }

            if (humanTeam is not null)
            {
                await ClubMoodService.CheckThresholds(humanTeam, state, _messages, state.CurrentDate);
                await ClubMoodService.CheckBoardMoodPraise(humanTeam, _messages, state.CurrentDate);
                await _expiryWarnings.CheckAsync(humanTeam, state.CurrentDate);
                await _finance.CheckFinanceWarningAsync(humanTeam, state.CurrentDate);
                if (runWeeklyTick)
                    await _finance.CheckSeasonEndProjectionAsync(humanTeam, state, state.CurrentDate);
                bool settledHuman = await _finance.ApplyMonthlySettlementAsync(humanTeam, state.CurrentDate);
                if (settledHuman)
                    FinanceService.ApplyFinancialHealthMoodCoupling(humanTeam);
                await _trainingCamps.ApplyDueCampsAsync(humanTeam, state.CurrentDate);
                if (_saveGame is not null)
                {
                    var scoutedIds = await _saveGame.ApplyDueScoutingAsync(humanTeam.Id, state.CurrentDate);
                    if (scoutedIds.Count > 0)
                    {
                        var scoutedIdSet = scoutedIds.ToHashSet();
                        foreach (var p in teams.SelectMany(t => t.Players.Concat(t.YouthPlayers)))
                            if (scoutedIdSet.Contains(p.Id))
                                p.IsScouted = true;
                    }

                    await _saveGame.ApplyScoutingFocusesAsync(humanTeam, teams, state.CurrentDate);
                }

                // Own players are always fully scouted - covers all acquisition paths (transfer,
                // loan, youth promotion) without patching each one individually.
                foreach (var p in humanTeam.Players.Concat(humanTeam.YouthPlayers))
                    p.IsScouted = true;

                touchedTeamIds.Add(humanTeam.Id);
            }

            foreach (var teamId in touchedTeamIds)
                await _teams.SaveTeamAsync(teamsById[teamId], includeYouth: false);
        }
    }
}
