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
    }
}
