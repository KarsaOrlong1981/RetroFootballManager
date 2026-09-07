using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class TransferHistoryRepository
    {
        private readonly AppDatabase _db;

        public TransferHistoryRepository(AppDatabase db)
        {
            _db = db;
        }

        public Task SaveAsync(TransferHistoryEntry entry) => _db.Connection.InsertAsync(entry);

        public Task<List<TransferHistoryEntry>> GetSinceAsync(DateTime since) =>
            _db.Connection.Table<TransferHistoryEntry>().Where(e => e.Date > since).ToListAsync();
    }
}
