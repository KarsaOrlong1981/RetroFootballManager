using RetroFootballManager.Common;
using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class CupTieRepository
    {
        private readonly AppDatabase _db;

        public CupTieRepository(AppDatabase db)
        {
            _db = db;
        }

        public Task<List<CupTie>> GetBySeasonAsync(int season, CompetitionType competition) =>
            _db.Connection.Table<CupTie>()
                .Where(t => t.Season == season && t.CompetitionType == competition)
                .ToListAsync();

        // Whether teamId never entered / is still in / got eliminated from / won this
        // competition in this season - derived from tie history, see CupParticipationService.
        public async Task<CupParticipationStatus> GetParticipationStatusAsync(int teamId, int season, CompetitionType competition)
        {
            var ties = await GetBySeasonAsync(season, competition);
            return CupParticipationService.GetStatus(teamId, ties);
        }

        public Task<List<CupTie>> GetByRoundAsync(int season, CompetitionType competition, int round) =>
            _db.Connection.Table<CupTie>()
                .Where(t => t.Season == season && t.CompetitionType == competition && t.Round == round)
                .ToListAsync();

        public Task InsertAllAsync(IEnumerable<CupTie> ties) =>
            _db.Connection.InsertAllAsync(ties);

        public async Task SaveAsync(CupTie tie)
        {
            var existing = tie.Id != 0
                ? await _db.Connection.FindAsync<CupTie>(tie.Id)
                : null;

            if (existing is null)
                await _db.Connection.InsertAsync(tie);
            else
                await _db.Connection.UpdateAsync(tie);
        }

        public Task<int> DeleteBySeasonAsync(int season) =>
            _db.Connection.Table<CupTie>().Where(t => t.Season == season).DeleteAsync();
    }
}
