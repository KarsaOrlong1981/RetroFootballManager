using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class PendingNegotiationRepository
    {
        private readonly AppDatabase _db;

        public PendingNegotiationRepository(AppDatabase db)
        {
            _db = db;
        }

        public Task<List<PendingNegotiation>> GetByTeamAsync(int teamId) =>
            _db.Connection.Table<PendingNegotiation>().Where(n => n.TeamId == teamId).ToListAsync();

        // Due for resolution on the daily tick - see NegotiationResolutionService.
        public Task<List<PendingNegotiation>> GetDueAsync(int teamId, DateTime currentDate) =>
            _db.Connection.Table<PendingNegotiation>()
                .Where(n => n.TeamId == teamId && n.DecisionDate <= currentDate).ToListAsync();

        public async Task SaveAsync(PendingNegotiation negotiation)
        {
            var existing = negotiation.Id != 0
                ? await _db.Connection.FindAsync<PendingNegotiation>(negotiation.Id)
                : null;

            if (existing is null)
                await _db.Connection.InsertAsync(negotiation);
            else
                await _db.Connection.UpdateAsync(negotiation);
        }

        public Task DeleteAsync(int id) => _db.Connection.DeleteAsync<PendingNegotiation>(id);
    }
}
