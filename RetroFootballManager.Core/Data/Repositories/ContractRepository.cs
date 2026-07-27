using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class ContractRepository
    {
        private readonly AppDatabase _db;

        public ContractRepository(AppDatabase db)
        {
            _db = db;
        }

        public Task<List<Contract>> GetByHolderAsync(int holderId, ContractHolderType type) =>
            _db.Connection.Table<Contract>()
                .Where(c => c.HolderId == holderId && c.HolderType == type).ToListAsync();

        public Task<List<Contract>> GetByTeamAsync(int teamId) =>
            _db.Connection.Table<Contract>().Where(c => c.TeamId == teamId).ToListAsync();

        public async Task<Contract?> GetByIdAsync(int id) => await _db.Connection.FindAsync<Contract>(id);

        public async Task SaveAsync(Contract contract)
        {
            var existing = contract.Id != 0
                ? await _db.Connection.FindAsync<Contract>(contract.Id)
                : null;

            if (existing is null)
                await _db.Connection.InsertAsync(contract);
            else
                await _db.Connection.UpdateAsync(contract);
        }

        public Task DeleteAsync(int id) => _db.Connection.DeleteAsync<Contract>(id);
    }
}
