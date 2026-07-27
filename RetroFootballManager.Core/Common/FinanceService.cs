using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public record MatchdayFinanceResult(int TicketIncome, int MerchandiseIncome, int SponsorWinBonus);

    // Books a team's ongoing income/expenses. Two separate triggers:
    // - ApplyMatchdayFinanceAsync: purely match-dependent items (ticket income only for
    //   home games, sponsor win bonus only on a win, merchandise per matchday).
    // - ApplyMonthlySettlementAsync: real calendar-month settlement (player/staff wages,
    //   stadium upkeep, sponsor monthly rate = annual amount/12 each), independent of
    //   whether a matchday is happening - also runs during preseason/winter break
    //   (called from both MatchDayService and CalendarAdvanceService).
    public class FinanceService
    {
        public const int MatchdaysPerSeason = 34;
        private const double MerchandisePerMatchday = 2_000;
        private const int FinanceWarningThreshold = 0;

        private readonly SponsorRepository _sponsorCatalog;
        private readonly SponsorshipRepository _sponsorships;
        private readonly ContractRepository _contracts;
        private readonly MessageService? _messages;

        public FinanceService(
            SponsorRepository sponsorCatalog, SponsorshipRepository sponsorships, ContractRepository contracts,
            MessageService? messages = null)
        {
            _sponsorCatalog = sponsorCatalog;
            _sponsorships = sponsorships;
            _contracts = contracts;
            _messages = messages;
        }

        // Warns once (FinanceWarning message) as soon as the balance drops below the
        // threshold; fires again only after it has recovered and drops again.
        public async Task CheckFinanceWarningAsync(Team team, DateTime currentDate)
        {
            var finances = team.Finances;
            if (_messages is null || finances is null)
                return;

            if (finances.CurrentBalance < FinanceWarningThreshold)
            {
                if (!finances.FinanceWarningActive)
                {
                    finances.FinanceWarningActive = true;
                    await _messages.SendAsync(MessageType.FinanceWarning, "Finanzen im Minus",
                        $"Der Kontostand ist auf {finances.CurrentBalance:N0} gefallen - handle, bevor es schlimmer wird.",
                        currentDate, team.Id);
                }
            }
            else
            {
                finances.FinanceWarningActive = false;
            }
        }

        // Purely match-dependent items - wages/stadium upkeep/sponsor base rate run
        // separately via ApplyMonthlySettlementAsync.
        public async Task<MatchdayFinanceResult> ApplyMatchdayFinanceAsync(
            Team team, bool isHome, bool won, IReadOnlyList<StandingRow> standings, int opponentTierRank,
            DateTime currentDate)
        {
            var finances = team.Finances;
            var stadium = team.Stadium;
            if (finances is null || stadium is null)
                return new MatchdayFinanceResult(0, 0, 0);

            int ticketIncome = isHome ? CalculateTicketIncome(team, stadium, standings, opponentTierRank) : 0;
            finances.TicketIncome += ticketIncome;

            int sponsorWinBonus = await CalculateSponsorWinBonusAsync(team, won);
            finances.SponsorIncome += sponsorWinBonus;

            int merchandiseIncome = (int)(MerchandisePerMatchday * stadium.MerchandiseLevel);
            finances.MerchandiseIncome += merchandiseIncome;

            finances.CurrentBalance += ticketIncome + sponsorWinBonus + merchandiseIncome;

            return new MatchdayFinanceResult(ticketIncome, merchandiseIncome, sponsorWinBonus);
        }

        // Real calendar-month settlement: fires once per calendar month once the 15th is
        // reached (independent of matchdays) - annual amount/12 for player/staff wages,
        // stadium upkeep, and sponsor base rate. Returns whether settlement actually happened.
        public async Task<bool> ApplyMonthlySettlementAsync(Team team, DateTime currentDate, bool sendMessage = true)
        {
            var finances = team.Finances;
            if (finances is null)
                return false;

            if (currentDate.Day < 15)
                return false;
            if (finances.LastSettlementMonth == currentDate.Month && finances.LastSettlementYear == currentDate.Year)
                return false;

            int playerWages = await CalculateMonthlyPlayerWagesAsync(team, currentDate);
            int staffWages = (int)Math.Round(team.Employees.Sum(e => e.Salary) / 12.0);
            int stadiumCost = team.Stadium is null ? 0 : (int)Math.Round(team.Stadium.MaintenanceCosts / 12.0);
            int sponsorIncome = await CalculateMonthlySponsorIncomeAsync(team);

            int loanPayment = 0;
            if (team.ActiveLoan is not null)
            {
                var result = ClubLoanService.ApplyMonthlyPayment(team.ActiveLoan, currentDate);
                if (result is { } r)
                    loanPayment = r.InterestPortion + r.PrincipalPortion;
            }

            finances.PlayerWages += playerWages;
            finances.StaffWages += staffWages;
            finances.StadiumCosts += stadiumCost;
            finances.SponsorIncome += sponsorIncome;
            finances.CurrentBalance += sponsorIncome - playerWages - staffWages - stadiumCost - loanPayment;

            finances.LastSettlementMonth = currentDate.Month;
            finances.LastSettlementYear = currentDate.Year;

            if (_messages is not null && sendMessage)
            {
                int net = sponsorIncome - playerWages - staffWages - stadiumCost - loanPayment;
                string sign = net >= 0 ? "+" : "";
                string loanLine = loanPayment > 0 ? $" · Kreditrate: -{loanPayment:N0} €" : "";
                await _messages.SendAsync(MessageType.CalendarAdvanceSummary, "Monatsabrechnung",
                    $"Spielergehälter: -{playerWages:N0} € · Mitarbeitergehälter: -{staffWages:N0} € · " +
                    $"Stadionunterhalt: -{stadiumCost:N0} € · Sponsoren: +{sponsorIncome:N0} €{loanLine} · Netto: {sign}{net:N0} €",
                    currentDate, team.Id);
            }

            return true;
        }

        private async Task<int> CalculateMonthlyPlayerWagesAsync(Team team, DateTime currentDate)
        {
            var contracts = await _contracts.GetByTeamAsync(team.Id);
            double annualTotal = contracts
                .Where(c => c.HolderType == ContractHolderType.Player && c.EndDate > currentDate)
                .Sum(c => c.AnnualSalary);

            return (int)Math.Round(annualTotal / 12.0);
        }

        private static int CalculateTicketIncome(
            Team team, Stadium stadium, IReadOnlyList<StandingRow> standings, int opponentTierRank)
        {
            var row = standings.FirstOrDefault(s => s.TeamId == team.Id);
            double formPoints = FormPoints(row?.Form ?? string.Empty);
            int leaguePosition = row?.Position ?? standings.Count;
            double baselinePrice = 10 + (4 - team.LeagueTier) * 8; // matches UniverseGenerator's tier pricing

            var attendance = AttendanceModel.Calculate(
                stadium, formPoints, leaguePosition, Math.Max(standings.Count, 1), opponentTierRank, baselinePrice);

            return (int)(attendance.SeatingSold * stadium.SeatPrice
                       + attendance.StandingSold * stadium.StandingPrice
                       + attendance.LogeSold * stadium.LogePrice);
        }

        private static double FormPoints(string form) =>
            form.Sum(c => c switch { 'W' => 3, 'D' => 1, _ => 0 });

        private async Task<int> CalculateSponsorWinBonusAsync(Team team, bool won)
        {
            if (!won)
                return 0;

            var deals = await _sponsorships.GetByTeamAsync(team.Id);
            if (deals.Count == 0)
                return 0;

            var catalog = await _sponsorCatalog.GetAllAsync();
            int bonus = 0;
            foreach (var deal in deals)
            {
                var sponsor = catalog.FirstOrDefault(s => s.Id == deal.SponsorId);
                if (sponsor is not null)
                    bonus += sponsor.BonusPerWin;
            }
            return bonus;
        }

        private async Task<int> CalculateMonthlySponsorIncomeAsync(Team team)
        {
            var deals = await _sponsorships.GetByTeamAsync(team.Id);
            if (deals.Count == 0)
                return 0;

            var catalog = await _sponsorCatalog.GetAllAsync();
            double income = 0;
            foreach (var deal in deals)
            {
                var sponsor = catalog.FirstOrDefault(s => s.Id == deal.SponsorId);
                if (sponsor is not null)
                    income += sponsor.SeasonPayment / 12.0;
            }
            return (int)Math.Round(income);
        }

        // Estimates the balance at season end. Two separate projections, since they now
        // follow different cadences: ticket/merchandise keep running per matchday (average
        // so far * remaining matchdays), wages/stadium/sponsor now run truly monthly
        // (ApplyMonthlySettlementAsync) - approximated by converting matchdays-played-as-
        // fraction-of-season into elapsed/remaining months, since this method gets no real
        // calendar date. Deliberately just an estimate ("approx.") - ticket income varies
        // with standings/form/opponent/attendance. Computed purely from already-booked
        // Finances fields, so it's instantly correct on the next call after ANY finance
        // change - no cached value.
        public static (int ProjectedBalance, bool IsReliable) EstimateSeasonEndBalance(
            Finances finances, int matchdaysPlayed, ClubLoan? activeLoan = null, int totalMatchdays = MatchdaysPerSeason)
        {
            if (matchdaysPlayed <= 0)
                return (finances.CurrentBalance, false);

            int remainingMatchdays = Math.Max(0, totalMatchdays - matchdaysPlayed);
            double matchNetSoFar = finances.TicketIncome + finances.MerchandiseIncome;
            double avgMatchNetPerMatchday = matchNetSoFar / matchdaysPlayed;
            double projectedMatchNet = avgMatchNetPerMatchday * remainingMatchdays;

            double monthsElapsed = matchdaysPlayed / (double)totalMatchdays * 12.0;
            double monthlyNetSoFar = finances.SponsorIncome - finances.StaffWages - finances.PlayerWages - finances.StadiumCosts;
            double avgNetPerMonth = monthsElapsed > 0 ? monthlyNetSoFar / monthsElapsed : 0;
            double remainingMonths = Math.Max(0, 12.0 - monthsElapsed);
            double projectedMonthlyNet = avgNetPerMonth * remainingMonths;

            int projected = finances.CurrentBalance + (int)Math.Round(projectedMatchNet + projectedMonthlyNet);

            if (activeLoan is { Status: ClubLoanStatus.Active })
            {
                double loanMonths = Math.Min(remainingMonths, ClubLoanService.EstimateMonthsRemaining(activeLoan));
                projected -= (int)Math.Round(loanMonths * activeLoan.MonthlyPayment);
            }

            return (projected, true);
        }

        // Season rollover: zero the season-tracked fields, ready for the next season's booking.
        public static void RolloverSeason(Finances finances)
        {
            finances.TransferIncome = 0;
            finances.TransferExpense = 0;
            finances.StadiumCosts = 0;
            finances.TicketIncome = 0;
            finances.SponsorIncome = 0;
            finances.MerchandiseIncome = 0;
            finances.StaffWages = 0;
            finances.PlayerWages = 0;
            finances.OtherIncome = 0;
            finances.OtherExpenses = 0;
        }
    }
}
