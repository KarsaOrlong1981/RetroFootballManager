using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    // Catalog of offerable sponsors (reference data, reseeded on every new game).
    public class SponsorRepository
    {
        private readonly AppDatabase _db;

        public SponsorRepository(AppDatabase db)
        {
            _db = db;
        }

        public Task<List<Sponsor>> GetAllAsync() =>
            _db.Connection.Table<Sponsor>().ToListAsync();

        public async Task SaveAsync(Sponsor sponsor)
        {
            var existing = sponsor.Id != 0
                ? await _db.Connection.FindAsync<Sponsor>(sponsor.Id)
                : null;

            if (existing is null)
                await _db.Connection.InsertAsync(sponsor);
            else
                await _db.Connection.UpdateAsync(sponsor);
        }
    }
}
