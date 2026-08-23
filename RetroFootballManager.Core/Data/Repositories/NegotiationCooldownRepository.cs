using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class NegotiationCooldownRepository
    {
        private readonly AppDatabase _db;

        public NegotiationCooldownRepository(AppDatabase db)
        {
            _db = db;
        }

        public Task<NegotiationCooldown?> GetActiveAsync(int buyingTeamId, int playerId, int season) =>
            _db.Connection.Table<NegotiationCooldown>()
                .Where(c => c.BuyingTeamId == buyingTeamId && c.PlayerId == playerId && c.Season == season)
                .FirstOrDefaultAsync();

        public Task SaveAsync(NegotiationCooldown cooldown) => _db.Connection.InsertAsync(cooldown);
    }
}
