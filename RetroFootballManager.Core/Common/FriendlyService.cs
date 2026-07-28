using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Friendlies (M7): can only be scheduled during pre-season/winter break (see
    // SeasonPhaseCalculator), played like a real match (career minutes, injuries), but WITHOUT
    // affecting the table/season stats (Fixture.IsFriendly). Scheduled for a date and played
    // there as a real live match (FriendlyMatchDayPage) - no more instant-simulating for the
    // user's own team.
    public class FriendlyService
    {
        private readonly FixtureRepository _fixtures;
        private readonly TeamRepository _teams;
        private readonly TrainingCampRepository _trainingCamps;
        private readonly MessageService _messages;
        private readonly Random _random;

        public FriendlyService(
            FixtureRepository fixtures, TeamRepository teams, TrainingCampRepository trainingCamps,
            MessageService messages, Random? random = null)
        {
            _fixtures = fixtures;
            _teams = teams;
            _trainingCamps = trainingCamps;
            _messages = messages;
            _random = random ?? Random.Shared;
        }

        public async Task<Fixture> ScheduleAsync(int season, Team homeTeam, Team awayTeam, DateTime date)
        {
            var fixture = new Fixture
            {
                Season = season,
                LeagueTier = homeTeam.LeagueTier,
                Matchday = 0,
                Date = date,
                HomeTeamId = homeTeam.Id,
                AwayTeamId = awayTeam.Id,
                Played = false,
                IsFriendly = true,
            };
            await _fixtures.SaveAsync(fixture);
            return fixture;
        }

        public Task<List<Fixture>> GetDueFriendliesAsync(int teamId, DateTime currentDate) =>
            _fixtures.GetFriendliesDueAsync(teamId, currentDate);

        // For an overview of "which friendlies are coming up" - all scheduled, not yet played
        // friendlies (not just the one already due).
        public Task<List<Fixture>> GetUpcomingFriendliesAsync(int teamId) =>
            _fixtures.GetUpcomingFriendliesAsync(teamId);

        // Friendlies only draw a fraction of a competitive match's crowd - simplified ticket
        // income (seating/seat price only, no table position/form influence like real league
        // games), calculated at the home team's stadium but split evenly between both clubs
        // (typical for friendly arrangements), booked as OtherIncome.
        private const double FriendlyAttendanceFraction = 0.2;

        public static int CalculateFriendlyIncome(Stadium? stadium) =>
            stadium is null ? 0 : (int)(stadium.SeatingCapacity * stadium.SeatPrice * FriendlyAttendanceFraction);

        public static void ApplyFriendlyIncome(Team homeTeam, Team awayTeam)
        {
            int total = CalculateFriendlyIncome(homeTeam.Stadium);
            if (total <= 0)
                return;

            int share = total / 2;
            if (homeTeam.Finances is not null)
            {
                homeTeam.Finances.CurrentBalance += share;
                homeTeam.Finances.OtherIncome += share;
            }
            if (awayTeam.Finances is not null)
            {
                awayTeam.Finances.CurrentBalance += share;
                awayTeam.Finances.OtherIncome += share;
            }
        }

        // Checks whether a friendly may be scheduled on this date: within the pre-season/winter
        // break window, no camp running, no other match that day.
        public async Task<(bool Allowed, string? Reason)> CanScheduleAsync(int teamId, DateTime date, DateTime? windowEnd)
        {
            if (windowEnd is null)
                return (false, "Freundschaftsspiele sind nur in Vorbereitung oder Winterpause möglich.");
            if (date.Date > windowEnd.Value.Date)
                return (false, "Datum liegt außerhalb der Vorbereitung/Winterpause.");

            var overlappingCamps = await _trainingCamps.GetOverlappingAsync(teamId, date);
            if (overlappingCamps.Count > 0)
                return (false, "An diesem Tag läuft ein Trainingslager.");

            if (await _fixtures.HasFixtureOnDateAsync(teamId, date))
                return (false, "An diesem Tag steht bereits ein anderes Spiel an.");

            return (true, null);
        }

        // Suggests up to `count` valid dates (skipping camp days and already booked days), for
        // the date picker in the friendly-scheduling dialog.
        public async Task<List<DateTime>> GetSuggestedDatesAsync(
            int teamId, DateTime currentDate, DateTime windowEnd, int count = 5)
        {
            var suggestions = new List<DateTime>();
            var candidate = currentDate.Date.AddDays(1);
            while (candidate <= windowEnd.Date && suggestions.Count < count)
            {
                var (allowed, _) = await CanScheduleAsync(teamId, candidate, windowEnd);
                if (allowed)
                    suggestions.Add(candidate);
                candidate = candidate.AddDays(1);
            }
            return suggestions;
        }

        public async Task<MatchResult> PlayDueFriendlyAsync(Fixture fixture, Team home, Team away, int humanTeamId)
        {
            MatchDayService.PrepareForMatch(home, fixture.Date, isFriendly: true);
            MatchDayService.PrepareForMatch(away, fixture.Date, isFriendly: true);

            var match = new Match(home, away, _random)
            {
                HomeCoach = new AiMatchCoach(),
                AwayCoach = new AiMatchCoach(),
            };
            var result = match.Simulate();

            fixture.HomeGoals = result.HomeGoals;
            fixture.AwayGoals = result.AwayGoals;
            fixture.Played = true;
            result.ApplyInjuryDurations(fixture.Date);
            await MatchDayService.NotifyInjuriesAsync(_messages, result, home, away, humanTeamId, fixture.Date);
            MatchDayService.ApplyCareerMinutes(result, home, away);
            ApplyFriendlyIncome(home, away);

            await _fixtures.SaveAsync(fixture);
            await _teams.SaveTeamAsync(home, includeYouth: false);
            await _teams.SaveTeamAsync(away, includeYouth: false);

            await _messages.SendAsync(MessageType.CalendarAdvanceSummary, "Freundschaftsspiel",
                $"{home.Name} {result.HomeGoals}:{result.AwayGoals} {away.Name}", fixture.Date, humanTeamId);

            return result;
        }
    }
}
