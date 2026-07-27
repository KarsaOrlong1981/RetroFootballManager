using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class CupTieRepository
    {
        private readonly AppDatabase _db;

        public CupTieRepository(AppDatabase db)
        {
            _db = db;
        }

        public Task<List<CupTie>> GetBySeasonAsync(int season, CompetitionType competition) =>
            _db.Connection.Table<CupTie>()
                .Where(t => t.Season == season && t.CompetitionType == competition)
                .ToListAsync();

        public Task<List<CupTie>> GetByRoundAsync(int season, CompetitionType competition, int round) =>
            _db.Connection.Table<CupTie>()
                .Where(t => t.Season == season && t.CompetitionType == competition && t.Round == round)
                .ToListAsync();

        public Task InsertAllAsync(IEnumerable<CupTie> ties) =>
            _db.Connection.InsertAllAsync(ties);

        public async Task SaveAsync(CupTie tie)
        {
            var existing = tie.Id != 0
                ? await _db.Connection.FindAsync<CupTie>(tie.Id)
                : null;

            if (existing is null)
                await _db.Connection.InsertAsync(tie);
            else
                await _db.Connection.UpdateAsync(tie);
        }

        public Task<int> DeleteBySeasonAsync(int season) =>
            _db.Connection.Table<CupTie>().Where(t => t.Season == season).DeleteAsync();
    }
}
