using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class ScoutingAssignmentRepository
    {
        private readonly AppDatabase _db;

        public ScoutingAssignmentRepository(AppDatabase db)
        {
            _db = db;
        }

        public Task<List<ScoutingAssignment>> GetByTeamAsync(int teamId) =>
            _db.Connection.Table<ScoutingAssignment>().Where(a => a.TeamId == teamId).ToListAsync();

        public Task<ScoutingAssignment?> GetForPlayerAsync(int teamId, int playerId) =>
            _db.Connection.Table<ScoutingAssignment>()
                .Where(a => a.TeamId == teamId && a.PlayerId == playerId).FirstOrDefaultAsync();

        public async Task SaveAsync(ScoutingAssignment assignment)
        {
            bool exists = assignment.Id != 0 && await _db.Connection.FindAsync<ScoutingAssignment>(assignment.Id) is not null;
            if (exists)
                await _db.Connection.UpdateAsync(assignment);
            else
                await _db.Connection.InsertAsync(assignment);
        }

        public Task DeleteAsync(int id) => _db.Connection.DeleteAsync<ScoutingAssignment>(id);
    }
}
