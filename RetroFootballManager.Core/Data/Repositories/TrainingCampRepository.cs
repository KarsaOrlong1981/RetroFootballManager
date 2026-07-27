using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class TrainingCampRepository
    {
        private readonly AppDatabase _db;

        public TrainingCampRepository(AppDatabase db)
        {
            _db = db;
        }

        public Task<List<TrainingCamp>> GetUnappliedByTeamAsync(int teamId) =>
            _db.Connection.Table<TrainingCamp>().Where(c => c.TeamId == teamId && !c.Applied).ToListAsync();

        public Task<List<TrainingCamp>> GetAllUnappliedAsync() =>
            _db.Connection.Table<TrainingCamp>().Where(c => !c.Applied).ToListAsync();

        // Blocks scheduling of friendlies on days when a camp is running.
        public Task<List<TrainingCamp>> GetOverlappingAsync(int teamId, DateTime date) =>
            _db.Connection.Table<TrainingCamp>()
                .Where(c => c.TeamId == teamId && c.StartDate <= date && date <= c.EndDate)
                .ToListAsync();

        public async Task SaveAsync(TrainingCamp camp)
        {
            if (camp.Id != 0)
                await _db.Connection.UpdateAsync(camp);
            else
                await _db.Connection.InsertAsync(camp);
        }
    }
}
