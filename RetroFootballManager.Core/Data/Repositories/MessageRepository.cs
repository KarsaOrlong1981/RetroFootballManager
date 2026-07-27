using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class MessageRepository
    {
        private readonly AppDatabase _db;

        public MessageRepository(AppDatabase db)
        {
            _db = db;
        }

        public Task<List<Message>> GetAllAsync() =>
            _db.Connection.Table<Message>().OrderByDescending(m => m.Date).ToListAsync();

        public Task<int> GetUnreadCountAsync() =>
            _db.Connection.Table<Message>().Where(m => !m.IsRead).CountAsync();

        public Task<List<Message>> GetByPlayerAndTypeAsync(int playerId, MessageType type) =>
            _db.Connection.Table<Message>()
                .Where(m => m.RelatedPlayerId == playerId && m.Type == type)
                .ToListAsync();

        public async Task SaveAsync(Message message)
        {
            if (message.Id != 0)
                await _db.Connection.UpdateAsync(message);
            else
                await _db.Connection.InsertAsync(message);
        }

        public Task<int> DeleteAsync(int messageId) =>
            _db.Connection.DeleteAsync<Message>(messageId);
    }
}
