using SQLite;

namespace RetroFootballManager.Models
{
    // An offer from another club for a listed player.
    public class TransferOffer
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int ListingId { get; set; }

        [Indexed]
        public int OfferingTeamId { get; set; }

        public double OfferedFee { get; set; }
        public double WageOffer { get; set; }
        public TransferOfferStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }

        // Fee (or wage, for a loan) the seller wants instead, set when Status is Countered.
        public double CounterFee { get; set; }

        // Set once the negotiation dialog's manager phase locks in this fee - until this
        // date, TransferAiService.EvaluateIncomingOffersAsync leaves the offer untouched
        // (neither auto-accepted nor rejected) instead of re-evaluating it, since the player-
        // side terms are still being negotiated/awaiting the PendingNegotiation's Bedenkzeit.
        // A rival offer on the same listing is still evaluated normally and can win it.
        public DateTime? LockedUntilDate { get; set; }
    }
}
