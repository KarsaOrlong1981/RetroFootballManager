using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public class CalendarService
    {
        private readonly FixtureRepository _fixtures;

        public CalendarService(FixtureRepository fixtures)
        {
            _fixtures = fixtures;
        }

        public async Task<int> GetMatchdayCountAsync(int season)
        {
            var all = await _fixtures.GetBySeasonAsync(season);
            return all.Count == 0 ? 0 : all.Max(f => f.Matchday);
        }

        public async Task<int?> GetNextMatchdayAsync(int season, DateTime currentDate)
        {
            var all = await _fixtures.GetBySeasonAsync(season);
            var upcoming = all
                .Where(f => !f.Played && f.Date.Date >= currentDate.Date)
                .OrderBy(f => f.Matchday)
                .ToList();

            return upcoming.Count == 0 ? null : upcoming.First().Matchday;
        }

        public Task<List<Fixture>> GetMatchdayFixturesAsync(int season, int matchday) =>
            _fixtures.GetByMatchdayAsync(season, matchday);

        // Season phase, transfer-window state for the given save (see SeasonPhaseCalculator).
        public async Task<SeasonPhaseInfo> GetSeasonPhaseAsync(GameState state)
        {
            var seasonFixtures = await _fixtures.GetBySeasonAsync(state.Season);
            return SeasonPhaseCalculator.Calculate(state.CurrentDate, state.MatchdayIndex, seasonFixtures);
        }
    }
}
