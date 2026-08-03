using System.Collections.ObjectModel;
using RetroFootballManager.Models;

namespace RetroFootballManager.ViewModels
{
    // Read-only display row for the Finances page's sponsor overview - one per active deal
    // (main/perimeter/kit), showing where the sponsor money actually comes from: the base
    // rate (paid monthly, capped at FinanceService.SponsorPaymentMonths per season) plus
    // every bonus that can still be earned, paid as one lump sum at season end.
    public class SponsorOverviewItem
    {
        public string SlotLabel { get; init; } = string.Empty;
        public string SponsorName { get; init; } = string.Empty;
        public string SeasonPaymentText { get; init; } = string.Empty;
        public string PaymentPerMonthText { get; init; } = string.Empty;
        public string ExpiresText { get; init; } = string.Empty;
        public ObservableCollection<BonusOffers> Offers { get; init; } = [];
    }
}
