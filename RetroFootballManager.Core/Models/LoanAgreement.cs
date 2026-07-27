using SQLite;

namespace RetroFootballManager.Models
{
    // An active loan: the player plays for LoanTeam until EndDate but remains owned by
    // OriginTeam (returns automatically, see TransferMarketService.ReturnExpiredLoansAsync).
    public class LoanAgreement
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int PlayerId { get; set; }

        public int OriginTeamId { get; set; }
        public int LoanTeamId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool Returned { get; set; }

        // On a loan, the loan club only takes on the negotiated salary, not the market value -
        // the existing contract moves to the loan club for the loan duration (TeamId +
        // AnnualSalary changed) and is reset to these original values on return (see
        // TransferMarketService.LoanOutAsync/ReturnExpiredLoansAsync).
        public int ContractId { get; set; }
        public double OriginalAnnualSalary { get; set; }
    }
}
