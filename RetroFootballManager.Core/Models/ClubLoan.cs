using SQLite;

namespace RetroFootballManager.Models
{
    public enum ClubLoanStatus { Active, PaidOff }

    // Bank loan (interest rate/term/repayment) for the team - deliberately not called
    // "Loan"/"LoanAgreement" since that's already the player loan (LoanAgreement.cs). A team has
    // only one active loan, so no dedicated repository - like Finances/Stadium, the row is
    // loaded/saved directly by TeamRepository.
    public class ClubLoan
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int TeamId { get; set; }

        public int PrincipalAmount { get; set; }
        public double AnnualInterestRatePercent { get; set; }
        public int TermMonths { get; set; }

        // Fixed annuity payment, calculated once when the loan is taken out - NEVER recalculated.
        // RemainingBalance is reduced each month only by the principal portion (int, never
        // float) - so the loan reaches exactly 0 after TermMonths payments, no rounding drift.
        public int MonthlyPayment { get; set; }
        public int RemainingBalance { get; set; }
        public DateTime StartDate { get; set; }
        public ClubLoanStatus Status { get; set; }

        // Own idempotency guard for the monthly payment, separate from
        // Finances.LastSettlementMonth/Year - the loan can be taken out mid-month, after the
        // Finances guard for that month has already been set.
        public int? LastPaymentMonth { get; set; }
        public int? LastPaymentYear { get; set; }
    }
}
