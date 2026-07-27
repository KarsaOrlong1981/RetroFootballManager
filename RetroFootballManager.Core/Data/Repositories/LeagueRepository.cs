using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class LeagueRepository
    {
        private readonly AppDatabase _db;

        public LeagueRepository(AppDatabase db)
        {
            _db = db;
        }

        public Task<List<League>> GetAllAsync() =>
            _db.Connection.Table<League>().ToListAsync();

        public Task<List<League>> GetBySeasonAsync(int season) =>
            _db.Connection.Table<League>().Where(l => l.Season == season).ToListAsync();

        public async Task SaveAsync(League league)
        {
            var existing = league.Id != 0
                ? await _db.Connection.FindAsync<League>(league.Id)
                : null;

            if (existing is null)
                await _db.Connection.InsertAsync(league);
            else
                await _db.Connection.UpdateAsync(league);
        }
    }
}
