using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class ContractBonusRepository
    {
        private readonly AppDatabase _db;

        public ContractBonusRepository(AppDatabase db)
        {
            _db = db;
        }

        public Task<List<ContractBonus>> GetByContractAsync(int contractId) =>
            _db.Connection.Table<ContractBonus>().Where(b => b.ContractId == contractId).ToListAsync();

        public Task SaveAsync(ContractBonus bonus) => _db.Connection.InsertAsync(bonus);

        public Task<int> DeleteByContractAsync(int contractId) =>
            _db.Connection.Table<ContractBonus>().Where(b => b.ContractId == contractId).DeleteAsync();
    }
}
