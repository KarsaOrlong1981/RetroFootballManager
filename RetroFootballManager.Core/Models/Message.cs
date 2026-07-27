using SQLite;

namespace RetroFootballManager.Models
{
    // A message in the user's team inbox (transfer offers, injuries, contract/loan expiry
    // warnings, financial warnings, calendar fast-forward summaries).
    public class Message
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public MessageType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public bool IsRead { get; set; }

        [Indexed]
        public int? RelatedTeamId { get; set; }
        [Indexed]
        public int? RelatedPlayerId { get; set; }

        // For ContractExpiringSoon/LoanExpiringSoon: which threshold (60/30/14 days) triggered
        // this message - prevents the same threshold from warning multiple times.
        public int? WarningThresholdDays { get; set; }
    }
}
