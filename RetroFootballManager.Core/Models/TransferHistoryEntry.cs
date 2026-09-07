using SQLite;

namespace RetroFootballManager.Models
{
    // A permanently completed transfer (loans/free agents excluded - see
    // TransferMarketService.AcceptOfferAsync) - written once at the moment the deal completes,
    // since the underlying TransferOffer/TransferListing rows are deleted right after and no
    // other durable record exists. Read back by TransferWindowDigestService to build the
    // end-of-window summary message.
    public class TransferHistoryEntry
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public DateTime Date { get; set; }

        public int PlayerId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public double PlayerRating { get; set; }

        public int FromTeamId { get; set; }
        public string FromTeamName { get; set; } = string.Empty;
        public int FromLeagueTier { get; set; }
        public int ToTeamId { get; set; }
        public string ToTeamName { get; set; } = string.Empty;
        public int ToLeagueTier { get; set; }

        public int Fee { get; set; }
    }
}
