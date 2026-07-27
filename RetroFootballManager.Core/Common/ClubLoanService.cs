using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Bank loan for the team - pure calculation logic, no DB access (analogous to
    // FinanceService.EstimateSeasonEndBalance). Annuity loan: the monthly payment is
    // calculated once at origination and never recalculated - every month the interest/
    // principal split is derived from the current (integer) RemainingBalance, so no
    // rounding drift accumulates over many months and the loan ends exactly at 0.
    public static class ClubLoanService
    {
        private const double RateFor12Months = 6.0;
        private const double RateFor24Months = 8.0;
        private const double RateFor36Months = 10.0; // longer term = higher interest rate

        private static readonly Dictionary<int, int> MaxLoanByLeagueTier = new()
        {
            [1] = 2_000_000,
            [2] = 1_000_000,
            [3] = 500_000,
            [4] = 250_000,
        };

        public static int GetMaxLoanAmount(Team team) =>
            MaxLoanByLeagueTier.GetValueOrDefault(team.LeagueTier, 250_000);

        public static double GetRateForTerm(int termMonths) => termMonths switch
        {
            12 => RateFor12Months,
            24 => RateFor24Months,
            36 => RateFor36Months,
            _ => RateFor24Months,
        };

        public static int CalculateMonthlyPayment(int principal, double annualRatePercent, int termMonths)
        {
            double monthlyRate = annualRatePercent / 100.0 / 12.0;
            return (int)Math.Round(principal * monthlyRate / (1 - Math.Pow(1 + monthlyRate, -termMonths)));
        }

        // Creates the loan and pays out the amount immediately (like transfers - "instant").
        // The caller saves team.ActiveLoan/team.Finances afterwards via the usual
        // SaveTeamAsync/SaveTeamProgressAsync paths.
        public static bool TryTakeLoan(Team team, int principal, int termMonths, DateTime currentDate, out string? error)
        {
            error = null;
            if (team.ActiveLoan is { Status: ClubLoanStatus.Active })
            {
                error = "Es läuft bereits ein Kredit.";
                return false;
            }
            if (principal <= 0 || principal > GetMaxLoanAmount(team))
            {
                error = "Kreditsumme übersteigt das erlaubte Maximum für diese Liga.";
                return false;
            }
            if (team.Finances is null)
            {
                error = "Keine Finanzdaten.";
                return false;
            }

            double rate = GetRateForTerm(termMonths);
            team.ActiveLoan = new ClubLoan
            {
                TeamId = team.Id,
                PrincipalAmount = principal,
                AnnualInterestRatePercent = rate,
                TermMonths = termMonths,
                MonthlyPayment = CalculateMonthlyPayment(principal, rate, termMonths),
                RemainingBalance = principal,
                StartDate = currentDate,
                Status = ClubLoanStatus.Active,
            };
            team.Finances.CurrentBalance += principal;
            return true;
        }

        // Pays off the loan early with a lump sum, provided the balance covers it -
        // a way out of debt.
        public static bool TryPayOffEarly(Team team, out string? error)
        {
            error = null;
            var loan = team.ActiveLoan;
            if (loan is not { Status: ClubLoanStatus.Active })
            {
                error = "Kein aktiver Kredit.";
                return false;
            }
            if (team.Finances is null || team.Finances.CurrentBalance < loan.RemainingBalance)
            {
                error = "Nicht genug Geld für die vollständige Ablösung.";
                return false;
            }

            team.Finances.CurrentBalance -= loan.RemainingBalance;
            loan.RemainingBalance = 0;
            loan.Status = ClubLoanStatus.PaidOff;
            return true;
        }

        // Monthly tick, called from FinanceService.ApplyMonthlySettlementAsync. Own
        // idempotency guard on the loan itself (see ClubLoan.LastPaymentMonth/Year).
        public static (int InterestPortion, int PrincipalPortion)? ApplyMonthlyPayment(ClubLoan loan, DateTime currentDate)
        {
            if (loan.Status != ClubLoanStatus.Active)
                return null;
            if (currentDate.Day < 15)
                return null;
            if (loan.LastPaymentMonth == currentDate.Month && loan.LastPaymentYear == currentDate.Year)
                return null;

            double monthlyRate = loan.AnnualInterestRatePercent / 100.0 / 12.0;
            int interest = (int)Math.Round(loan.RemainingBalance * monthlyRate);
            int principal = Math.Min(loan.MonthlyPayment - interest, loan.RemainingBalance);

            loan.RemainingBalance -= principal;
            loan.LastPaymentMonth = currentDate.Month;
            loan.LastPaymentYear = currentDate.Year;
            if (loan.RemainingBalance <= 0)
            {
                loan.RemainingBalance = 0;
                loan.Status = ClubLoanStatus.PaidOff;
            }

            return (interest, principal);
        }

        public static int EstimateMonthsRemaining(ClubLoan loan) =>
            loan.MonthlyPayment <= 0 ? 0 : (int)Math.Ceiling(loan.RemainingBalance / (double)loan.MonthlyPayment);
    }
}
