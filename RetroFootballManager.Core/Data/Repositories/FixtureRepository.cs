using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class FixtureRepository
    {
        private readonly AppDatabase _db;

        public FixtureRepository(AppDatabase db)
        {
            _db = db;
        }

        public Task<List<Fixture>> GetAllAsync() =>
            _db.Connection.Table<Fixture>().ToListAsync();

        public Task<Fixture?> GetByIdAsync(int id) => _db.Connection.FindAsync<Fixture>(id)!;

        // Only real league fixtures - friendlies (IsFriendly) have no effect on the standings/
        // matchday flow/season phase and are deliberately excluded here.
        public Task<List<Fixture>> GetBySeasonAsync(int season) =>
            _db.Connection.Table<Fixture>().Where(f => f.Season == season && !f.IsFriendly).ToListAsync();

        public Task<List<Fixture>> GetByLeagueAsync(int season, int leagueTier) =>
            _db.Connection.Table<Fixture>()
                .Where(f => f.Season == season && f.LeagueTier == leagueTier && !f.IsFriendly)
                .ToListAsync();

        public Task<List<Fixture>> GetFriendliesDueAsync(int teamId, DateTime date) =>
            _db.Connection.Table<Fixture>()
                .Where(f => f.IsFriendly && !f.Played && f.Date <= date && (f.HomeTeamId == teamId || f.AwayTeamId == teamId))
                .ToListAsync();

        // All not-yet-played friendlies of the team, regardless of whether already due - for
        // an overview of "which friendlies are upcoming" (not just the next due one).
        public Task<List<Fixture>> GetUpcomingFriendliesAsync(int teamId) =>
            _db.Connection.Table<Fixture>()
                .Where(f => f.IsFriendly && !f.Played && (f.HomeTeamId == teamId || f.AwayTeamId == teamId))
                .ToListAsync();

        // For friendly scheduling: does the team already have a fixture on this day
        // (league/cup/friendly, regardless of season)?
        public async Task<bool> HasFixtureOnDateAsync(int teamId, DateTime date)
        {
            var teamFixtures = await _db.Connection.Table<Fixture>()
                .Where(f => f.HomeTeamId == teamId || f.AwayTeamId == teamId)
                .ToListAsync();
            return teamFixtures.Any(f => f.Date.Date == date.Date);
        }

        // For training camp booking: does ANY (not-yet-played) fixture of the team fall
        // within the planned camp period? Symmetric to CanScheduleAsync's training-camp
        // conflict check for friendlies - previously you could book a training camp that
        // overlapped an already scheduled friendly.
        public async Task<bool> HasFixtureInRangeAsync(int teamId, DateTime start, DateTime end)
        {
            var teamFixtures = await _db.Connection.Table<Fixture>()
                .Where(f => !f.Played && (f.HomeTeamId == teamId || f.AwayTeamId == teamId))
                .ToListAsync();
            return teamFixtures.Any(f => f.Date.Date >= start.Date && f.Date.Date <= end.Date);
        }

        public Task<List<Fixture>> GetByMatchdayAsync(int season, int matchday) =>
            _db.Connection.Table<Fixture>()
                .Where(f => f.Season == season && f.Matchday == matchday)
                .ToListAsync();

        // Inserts many fixtures efficiently (fixture list at season start).
        public Task InsertAllAsync(IEnumerable<Fixture> fixtures) =>
            _db.Connection.InsertAllAsync(fixtures);

        public async Task SaveAsync(Fixture fixture)
        {
            var existing = fixture.Id != 0
                ? await _db.Connection.FindAsync<Fixture>(fixture.Id)
                : null;

            if (existing is null)
                await _db.Connection.InsertAsync(fixture);
            else
                await _db.Connection.UpdateAsync(fixture);
        }

        public Task<int> DeleteBySeasonAsync(int season) =>
            _db.Connection.Table<Fixture>().Where(f => f.Season == season).DeleteAsync();
    }
}
