using SQLite;

namespace RetroFootballManager.Models
{
    public class Contract
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int HolderId { get; set; }         // PlayerId or EmployeeId, depending on HolderType
        public ContractHolderType HolderType { get; set; }

        [Indexed]
        public int TeamId { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public double AnnualSalary { get; set; }
        public double MarketValue { get; set; }
        public double SigningBonus { get; set; }

        // Exit clause: 0 = fixed contract (no clause), >0 = minimum offer amount a buyer
        // must meet for the club to be forced to sell (see PlayerTermsExpectationService).
        public double ReleaseClause { get; set; }

        // Squad role agreed in the negotiation (see RoleInTeam/PlayerTermsExpectationService).
        public RoleInTeam RoleInTeam { get; set; }

        // Percentage (0-30) of a future resale fee owed back to the selling club - agreed
        // with the selling club's manager, not the player (see NegotiationExpectationService).
        public double SellOnPercentage { get; set; }

        // True once any negotiated term above was actually set through the negotiation
        // dialog (see NegotiationResolutionService) - lets the UI tell a real (possibly
        // still-default-valued) negotiated field apart from a legacy/never-negotiated
        // contract that just happens to have RoleInTeam at its enum default.
        public bool HasNegotiatedTerms { get; set; }
    }
}
