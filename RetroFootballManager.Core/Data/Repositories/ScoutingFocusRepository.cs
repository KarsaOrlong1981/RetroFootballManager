using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class ScoutingFocusRepository
    {
        private readonly AppDatabase _db;

        public ScoutingFocusRepository(AppDatabase db)
        {
            _db = db;
        }

        public Task<List<ScoutingFocus>> GetByTeamAsync(int teamId) =>
            _db.Connection.Table<ScoutingFocus>().Where(f => f.TeamId == teamId).ToListAsync();

        public Task<ScoutingFocus?> GetForScoutAsync(int scoutEmployeeId) =>
            _db.Connection.Table<ScoutingFocus>()
                .Where(f => f.ScoutEmployeeId == scoutEmployeeId).FirstOrDefaultAsync();

        public async Task SaveAsync(ScoutingFocus focus)
        {
            bool exists = focus.Id != 0 && await _db.Connection.FindAsync<ScoutingFocus>(focus.Id) is not null;
            if (exists)
                await _db.Connection.UpdateAsync(focus);
            else
                await _db.Connection.InsertAsync(focus);
        }

        public Task DeleteAsync(int id) => _db.Connection.DeleteAsync<ScoutingFocus>(id);

        public Task DeleteForScoutAsync(int scoutEmployeeId) =>
            _db.Connection.Table<ScoutingFocus>().Where(f => f.ScoutEmployeeId == scoutEmployeeId).DeleteAsync();
    }
}
