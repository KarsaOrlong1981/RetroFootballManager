using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class LoanAgreementRepository
    {
        private readonly AppDatabase _db;

        public LoanAgreementRepository(AppDatabase db)
        {
            _db = db;
        }

        public Task<List<LoanAgreement>> GetActiveAsync() =>
            _db.Connection.Table<LoanAgreement>().Where(l => !l.Returned).ToListAsync();

        public Task<List<LoanAgreement>> GetByTeamAsync(int teamId) =>
            _db.Connection.Table<LoanAgreement>()
                .Where(l => !l.Returned && (l.OriginTeamId == teamId || l.LoanTeamId == teamId))
                .ToListAsync();

        public async Task SaveAsync(LoanAgreement loan)
        {
            var existing = loan.Id != 0
                ? await _db.Connection.FindAsync<LoanAgreement>(loan.Id)
                : null;

            if (existing is null)
                await _db.Connection.InsertAsync(loan);
            else
                await _db.Connection.UpdateAsync(loan);
        }
    }
}
