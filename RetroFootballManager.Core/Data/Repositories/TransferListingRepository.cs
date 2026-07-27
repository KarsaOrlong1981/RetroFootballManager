using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class TransferListingRepository
    {
        private readonly AppDatabase _db;

        public TransferListingRepository(AppDatabase db)
        {
            _db = db;
        }

        public Task<List<TransferListing>> GetBySeasonAsync(int season) =>
            _db.Connection.Table<TransferListing>().Where(l => l.Season == season).ToListAsync();

        public Task<List<TransferListing>> GetByTeamAsync(int teamId) =>
            _db.Connection.Table<TransferListing>().Where(l => l.TeamId == teamId).ToListAsync();

        public async Task<TransferListing?> GetByPlayerAsync(int playerId) =>
            await _db.Connection.Table<TransferListing>().Where(l => l.PlayerId == playerId).FirstOrDefaultAsync();

        public async Task SaveAsync(TransferListing listing)
        {
            var existing = listing.Id != 0
                ? await _db.Connection.FindAsync<TransferListing>(listing.Id)
                : null;

            if (existing is null)
                await _db.Connection.InsertAsync(listing);
            else
                await _db.Connection.UpdateAsync(listing);
        }

        public Task DeleteAsync(int id) => _db.Connection.DeleteAsync<TransferListing>(id);
    }
}
