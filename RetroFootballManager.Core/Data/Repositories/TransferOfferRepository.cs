using RetroFootballManager.Models;

namespace RetroFootballManager.Data.Repositories
{
    public class TransferOfferRepository
    {
        private readonly AppDatabase _db;

        public TransferOfferRepository(AppDatabase db)
        {
            _db = db;
        }

        public async Task<TransferOffer?> GetByIdAsync(int id) => await _db.Connection.FindAsync<TransferOffer>(id);

        public Task<List<TransferOffer>> GetByListingAsync(int listingId) =>
            _db.Connection.Table<TransferOffer>().Where(o => o.ListingId == listingId).ToListAsync();

        // Offers the team made that still need attention: Pending (awaiting the seller) or
        // Countered (awaiting the buyer's accept/reject of the seller's counter-fee).
        public Task<List<TransferOffer>> GetPendingByTeamAsync(int offeringTeamId) =>
            _db.Connection.Table<TransferOffer>()
                .Where(o => o.OfferingTeamId == offeringTeamId
                    && (o.Status == TransferOfferStatus.Pending || o.Status == TransferOfferStatus.Countered))
                .ToListAsync();

        public async Task SaveAsync(TransferOffer offer)
        {
            var existing = offer.Id != 0
                ? await _db.Connection.FindAsync<TransferOffer>(offer.Id)
                : null;

            if (existing is null)
                await _db.Connection.InsertAsync(offer);
            else
                await _db.Connection.UpdateAsync(offer);
        }

        public Task<int> DeleteByListingAsync(int listingId) =>
            _db.Connection.Table<TransferOffer>().Where(o => o.ListingId == listingId).DeleteAsync();
    }
}
