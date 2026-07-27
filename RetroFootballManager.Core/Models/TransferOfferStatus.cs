namespace RetroFootballManager.Models
{
    public enum TransferOfferStatus
    {
        Pending,
        Accepted,
        Rejected,
        Withdrawn,
        // Seller wants a higher fee/wage than offered - see TransferOffer.CounterFee. Ends in
        // Accepted (buyer agrees) or Rejected (buyer declines), never re-evaluated by the AI.
        Countered,
    }
}
