using System.Text.Json;
using SQLite;

namespace RetroFootballManager.Models
{
    // A concluded negotiation dialog awaiting its "Bedenkzeit" (think-it-over period, 3-4
    // days) before it actually takes effect - see NegotiationResolutionService. For
    // NegotiationKind.TransferOrLoanBuy, TransferOfferId points at the locked TransferOffer
    // (see TransferOffer.LockedUntilDate); for ContractRenewal, PlayerId/TeamId/ContractId
    // identify the existing contract being renewed instead.
    public class PendingNegotiation
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public NegotiationKind Kind { get; set; }

        [Indexed]
        public int? TransferOfferId { get; set; }

        [Indexed]
        public int PlayerId { get; set; }

        [Indexed]
        public int TeamId { get; set; }

        public int? ContractId { get; set; }

        public DateTime CreatedDate { get; set; }
        public DateTime DecisionDate { get; set; }

        // Agreed personal terms, materialized into the Contract (+ ContractBonus rows) once
        // resolved - see NegotiationResolutionService.
        public RoleInTeam RoleInTeam { get; set; }
        public int ContractYears { get; set; }

        // Loan duration in months - TransferOrLoanBuy + listing.IsLoanListing only. A loan
        // never negotiates RoleInTeam/ExitClauseAmount/SellOnPercentage/Bonuses (temporary
        // spell on someone else's contract, see NegotiationResolutionService).
        public int? LoanDurationMonths { get; set; }

        public double ExitClauseAmount { get; set; }
        public double SellOnPercentage { get; set; }

        // Only meaningful for ContractRenewal - TransferOrLoanBuy carries the wage on the
        // linked TransferOffer.WageOffer instead.
        public double NegotiatedWage { get; set; }

        private List<NegotiatedBonusLine>? _bonusesCache;
        private string _bonusesRaw = "[]";

        // Persisted as JSON (same convention as Player.SecondaryPositionsRaw) since
        // sqlite-net can't store a list directly and this is short-lived data.
        public string BonusesJson
        {
            get => _bonusesRaw;
            set { _bonusesRaw = value; _bonusesCache = null; }
        }

        [Ignore]
        public List<NegotiatedBonusLine> Bonuses
        {
            get => _bonusesCache ??= JsonSerializer.Deserialize<List<NegotiatedBonusLine>>(_bonusesRaw) ?? [];
            set { BonusesJson = JsonSerializer.Serialize(value); _bonusesCache = value; }
        }
    }
}
