using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class GameStateRepository
    {
        private const int SingletonId = 1;
        private readonly AppDatabase _db;

        public GameStateRepository(AppDatabase db)
        {
            _db = db;
        }

        public async Task<GameState?> GetAsync() =>
            await _db.Connection.FindAsync<GameState>(SingletonId);

        public async Task SaveAsync(GameState state)
        {
            state.Id = SingletonId;
            var existing = await GetAsync();
            if (existing is null)
                await _db.Connection.InsertAsync(state);
            else
                await _db.Connection.UpdateAsync(state);
        }

        public Task<int> DeleteAsync() =>
            _db.Connection.DeleteAsync<GameState>(SingletonId);
    }
}
