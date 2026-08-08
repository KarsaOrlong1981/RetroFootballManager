using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class PlayerRepository
    {
        private readonly AppDatabase _db;

        public PlayerRepository(AppDatabase db)
        {
            _db = db;
        }

        public Task<List<Player>> GetPlayersByTeamAsync(int teamId) =>
            _db.Connection.Table<Player>().Where(p => p.TeamId == teamId).ToListAsync();

        public async Task<Player?> GetPlayerAsync(int playerId) =>
            await _db.Connection.FindAsync<Player>(playerId);

        public async Task SavePlayerAsync(Player player)
        {
            bool exists = player.Id != 0 && await _db.Connection.FindAsync<Player>(player.Id) is not null;
            if (exists)
                await _db.Connection.UpdateAsync(player);
            else
                await _db.Connection.InsertAsync(player);
        }

        public Task<List<PlayerStats>> GetPlayerStatsAsync(int playerId, int season, CompetitionType? competition = null) =>
            _db.Connection.Table<PlayerStats>()
                .Where(s => s.PlayerId == playerId && s.Season == season && s.Competition == competition).ToListAsync();

        public Task<List<PlayerStats>> GetAllStatsAsync(int playerId) =>
            _db.Connection.Table<PlayerStats>().Where(s => s.PlayerId == playerId).ToListAsync();

        // All-time (every season) rows for one Competition value - null = league, a real
        // CompetitionType = that specific cup/friendly bucket. Used to build career totals
        // scoped to a single competition instead of GetAllStatsAsync's "everything" blend.
        public Task<List<PlayerStats>> GetAllStatsByCompetitionAsync(int playerId, CompetitionType? competition) =>
            _db.Connection.Table<PlayerStats>()
                .Where(s => s.PlayerId == playerId && s.Competition == competition).ToListAsync();

        public Task<List<PlayerStats>> GetStatsByCompetitionAsync(int season, CompetitionType competition) =>
            _db.Connection.Table<PlayerStats>()
                .Where(s => s.Season == season && s.Competition == competition).ToListAsync();

        public async Task SavePlayerStatsAsync(PlayerStats stats)
        {
            bool exists = stats.Id != 0 && await _db.Connection.FindAsync<PlayerStats>(stats.Id) is not null;
            if (exists)
                await _db.Connection.UpdateAsync(stats);
            else
                await _db.Connection.InsertAsync(stats);
        }
    }
}
