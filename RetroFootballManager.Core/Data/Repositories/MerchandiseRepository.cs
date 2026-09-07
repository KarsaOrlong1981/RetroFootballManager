using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class MerchandiseRepository
    {
        private readonly AppDatabase _db;

        public MerchandiseRepository(AppDatabase db)
        {
            _db = db;
        }

        public Task<MerchandiseInventory?> GetByTeamAsync(int teamId) =>
            _db.Connection.Table<MerchandiseInventory>().Where(m => m.TeamId == teamId).FirstOrDefaultAsync();

        public async Task SaveAsync(MerchandiseInventory inventory)
        {
            var existing = inventory.Id != 0
                ? await _db.Connection.FindAsync<MerchandiseInventory>(inventory.Id)
                : null;

            if (existing is null)
                await _db.Connection.InsertAsync(inventory);
            else
                await _db.Connection.UpdateAsync(inventory);
        }
    }
}
