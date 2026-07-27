using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class ScoutedPlayerRepository
    {
        private readonly AppDatabase _db;

        public ScoutedPlayerRepository(AppDatabase db)
        {
            _db = db;
        }

        public Task<List<ScoutedPlayer>> GetByTeamAsync(int teamId) =>
            _db.Connection.Table<ScoutedPlayer>().Where(p => p.TeamId == teamId).ToListAsync();

        public Task SaveAsync(ScoutedPlayer scouted) => _db.Connection.InsertAsync(scouted);

        public async Task RemoveAsync(int teamId, int playerId)
        {
            var rows = await _db.Connection.Table<ScoutedPlayer>()
                .Where(p => p.TeamId == teamId && p.PlayerId == playerId).ToListAsync();
            foreach (var row in rows)
                await _db.Connection.DeleteAsync(row);
        }
    }
}
