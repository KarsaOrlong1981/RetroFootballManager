using SQLite;

namespace RetroFootballManager.Models
{
    // Ein Spieler, den sein Verein auf dem Transfermarkt anbietet (Verkauf oder Leihe).
    public class TransferListing
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int PlayerId { get; set; }

        [Indexed]
        public int TeamId { get; set; }

        public double AskingPrice { get; set; }
        public bool IsLoanListing { get; set; }
        public int Season { get; set; }
        public DateTime ListedDate { get; set; }

        // True for a shadow listing created behind an unsolicited offer for a player the seller
        // never put up for transfer (found only via scouting) - never shown as a public market
        // listing, and needs a higher offer to be accepted (see TransferAiService.ShouldAcceptOffer).
        public bool IsUnsolicited { get; set; }
    }
}
