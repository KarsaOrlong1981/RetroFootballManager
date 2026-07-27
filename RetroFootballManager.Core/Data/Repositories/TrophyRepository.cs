using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class TrophyRepository
    {
        private readonly AppDatabase _db;

        public TrophyRepository(AppDatabase db)
        {
            _db = db;
        }

        public Task<List<TrophyRecord>> GetByTeamAsync(int teamId) =>
            _db.Connection.Table<TrophyRecord>().Where(t => t.TeamId == teamId).ToListAsync();

        public async Task RecordWinAsync(int teamId, TrophyType type, int season)
        {
            var existing = await _db.Connection.Table<TrophyRecord>()
                .Where(t => t.TeamId == teamId && t.Type == type).FirstOrDefaultAsync();

            if (existing is null)
            {
                await _db.Connection.InsertAsync(new TrophyRecord
                {
                    TeamId = teamId, Type = type, Count = 1, LastWonSeason = season,
                });
            }
            else
            {
                existing.Count += 1;
                existing.LastWonSeason = season;
                await _db.Connection.UpdateAsync(existing);
            }
        }
    }
}
