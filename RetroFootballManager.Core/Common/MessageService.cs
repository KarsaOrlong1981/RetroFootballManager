using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // The user team's inbox: central place where other services (transfers, injuries,
    // contract/loan expiry, finances, calendar advance) create messages.
    public class MessageService
    {
        private readonly MessageRepository _messages;

        public MessageService(MessageRepository messages)
        {
            _messages = messages;
        }

        public async Task<Message> SendAsync(
            MessageType type, string title, string body, DateTime date,
            int? relatedTeamId = null, int? relatedPlayerId = null, int? warningThresholdDays = null)
        {
            var message = new Message
            {
                Type = type,
                Title = title,
                Body = body,
                Date = date,
                IsRead = false,
                RelatedTeamId = relatedTeamId,
                RelatedPlayerId = relatedPlayerId,
                WarningThresholdDays = warningThresholdDays,
            };
            await _messages.SaveAsync(message);
            return message;
        }

        public Task<List<Message>> GetInboxAsync() => _messages.GetAllAsync();

        public Task<int> GetUnreadCountAsync() => _messages.GetUnreadCountAsync();

        public Task MarkReadAsync(Message message)
        {
            message.IsRead = true;
            return _messages.SaveAsync(message);
        }

        public Task DeleteAsync(Message message) => _messages.DeleteAsync(message.Id);

        // Checks whether this player/threshold has already been warned about (prevents
        // ContractExpiringSoon/LoanExpiringSoon from firing again every day).
        public async Task<bool> HasWarnedAsync(int playerId, MessageType type, int thresholdDays)
        {
            var existing = await _messages.GetByPlayerAndTypeAsync(playerId, type);
            return existing.Any(m => m.WarningThresholdDays == thresholdDays);
        }
    }
}
