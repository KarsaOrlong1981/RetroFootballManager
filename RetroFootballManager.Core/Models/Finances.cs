using SQLite;

namespace RetroFootballManager.Models
{
    public class Finances
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int TeamId { get; set; }

        // Basic team budget
        public int CurrentBalance { get; set; }          // Available money
        public int SeasonBudget { get; set; }            // Planned budget for the season

        // Income streams
        public int TicketIncome { get; set; }            // Matchday revenue
        public int SponsorIncome { get; set; }           // Sponsorship deals
        public int MerchandiseIncome { get; set; }       // Shirts, scarves, retro stuff

        // Expenses
        public int PlayerWages { get; set; }             // Total weekly/monthly wages
        public int StaffWages { get; set; }              // Coaches, scouts, etc.
        public int FacilityCosts { get; set; }           // Stadium, training ground
        public int YouthDevelopmentCosts { get; set; }   // Youth academy investment

        // Transfer-related
        public int TransferBudget { get; set; }          // Money available for transfers
        public int WageBudget { get; set; }              // Max wage capacity

        // Season tracking
        public int TransferIncome { get; set; }          // Season-to-date sales
        public int TransferExpense { get; set; }         // Season-to-date buys
        public int StadiumCosts { get; set; }            // Season-to-date maintenance paid

        // Other income/expenses (M7): friendly match ticket income, training camp costs.
        public int OtherIncome { get; set; }
        public int OtherExpenses { get; set; }

        // Retro-style financial health indicator
        public int FinancialHealth { get; set; }         // 0–100: simple retro rating

        // Prevents re-warning daily (FinanceWarning message) while the balance stays below the
        // threshold - only warns again after recovering above it and dropping below again
        // (see FinanceService.CheckFinanceWarningAsync).
        public bool FinanceWarningActive { get; set; }

        // Last calendar month/year for which the monthly settlement (player/staff wages,
        // stadium upkeep, monthly sponsor payment) has already been booked - prevents double
        // booking when the 15th is "touched" multiple times in the same month (day advance +
        // matchday in the same week). See FinanceService.ApplyMonthlySettlementAsync.
        public int? LastSettlementMonth { get; set; }
        public int? LastSettlementYear { get; set; }

        // How many of the season's monthly sponsor installments (see
        // FinanceService.SponsorPaymentMonths) have already been paid - caps sponsor income at
        // exactly SeasonPayment per season instead of it drifting on across the summer break,
        // where the monthly settlement still fires but no season is running. Reset in
        // FinanceService.RolloverSeason.
        public int SponsorPaymentsThisSeason { get; set; }
    }
}
