using System.Collections.ObjectModel;
using System.Text.Json;
using RetroFootballManager.Core.Common;
using RetroFootballManager.Core.Models;
using SQLite;

namespace RetroFootballManager.Models
{
    // Catalog/reference entry: a sponsor that can be offered to teams, not an active deal.
    public class Sponsor
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public SponsorType SponsorType { get; set; }

        // Persisted as JSON since sqlite-net can't store complex arrays directly.
        public string BonusTypesRaw { get; set; } = "[]";

        [Ignore]
        public BonusType[] BonusTypes
        {
            get => JsonSerializer.Deserialize<BonusType[]>(BonusTypesRaw) ?? [];
            set => BonusTypesRaw = JsonSerializer.Serialize(value);
        }

        public string ImagePath { get; set; } = string.Empty;
        public int MinTier { get; set; }              // weakest league tier (highest number) that still qualifies

        public int SeasonPayment { get; set; }
        public int BonusPerWin { get; set; }
        public int BonusPerPromotion { get; set; }
        public int BonusForMidfieldPlace { get; set; }
        public int BonusForTop5 { get; set; }
        public int BonusForMasterTitle { get; set; }
        public bool HasNoBonus { get; set; }

        // Split across the season's actual monthly settlements (see
        // FinanceService.SponsorPaymentMonths), not a full calendar year - matches how
        // CalculateMonthlySponsorIncomeAsync actually pays it out.
        [Ignore]
        public int PaymentPerMonth => SeasonPayment / RetroFootballManager.Common.FinanceService.SponsorPaymentMonths;

        [Ignore]
        public ObservableCollection<BonusOffers> Offers => DisplayBonusOffers();

        private ObservableCollection<BonusOffers> DisplayBonusOffers()
        {
            var offers = new ObservableCollection<BonusOffers>();
            offers.Add(new BonusOffers { Offer = BonusTypeInfo.GetDisplayName(BonusType.PerWin), Payment = BonusPerWin });
            foreach (var offer in BonusTypes)
            {
                if (offer == BonusType.None)
                    continue;

                var displayname = BonusTypeInfo.GetDisplayName(offer);
                var payment = GetPaymentForOffer(offer);

                offers.Add(new BonusOffers { Offer = displayname, Payment = payment});
            }

            return offers;
        }

        private int GetPaymentForOffer(BonusType bonus)
        {
            return bonus switch
            {
                BonusType.PerPromotion => BonusPerPromotion,
                BonusType.Top5 => BonusForTop5,
                BonusType.Midfield => BonusForMidfieldPlace,
                BonusType.MasterTitle => BonusForMasterTitle,
                BonusType.PerWin => BonusPerWin,
                _ => 0
            };
        }
    }
}
