using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class SponsorshipRepository
    {
        private readonly AppDatabase _db;

        public SponsorshipRepository(AppDatabase db)
        {
            _db = db;
        }

        public Task<List<Sponsorship>> GetByTeamAsync(int teamId) =>
            _db.Connection.Table<Sponsorship>().Where(s => s.TeamId == teamId).ToListAsync();

        public async Task SaveAsync(Sponsorship sponsorship)
        {
            var existing = sponsorship.Id != 0
                ? await _db.Connection.FindAsync<Sponsorship>(sponsorship.Id)
                : null;

            if (existing is null)
                await _db.Connection.InsertAsync(sponsorship);
            else
                await _db.Connection.UpdateAsync(sponsorship);
        }
    }
}
