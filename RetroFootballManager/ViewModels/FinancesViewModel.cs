using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Common;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public partial class FinancesViewModel : BaseViewModel
    {
        private readonly GameSession _session;
        private readonly INavigationService _navigation;
        private readonly SponsorshipRepository _sponsorshipRepository;
        private readonly SponsorRepository _sponsorRepository;
        private readonly ContractRepository _contractRepository;
        private readonly FinanceService _financeService;

        public FinancesViewModel(
            IDispatcher dispatcher, GameSession session, INavigationService navigation,
            SponsorshipRepository sponsorshipRepository, SponsorRepository sponsorRepository,
            ContractRepository contractRepository, FinanceService financeService)
            : base(dispatcher)
        {
            _session = session;
            _navigation = navigation;
            _sponsorshipRepository = sponsorshipRepository;
            _sponsorRepository = sponsorRepository;
            _contractRepository = contractRepository;
            _financeService = financeService;
            Title = "Finanzen";
        }

        [ObservableProperty] private string _balanceText = string.Empty;
        [ObservableProperty] private string _ticketIncomeText = string.Empty;
        [ObservableProperty] private string _sponsorIncomeText = string.Empty;
        [ObservableProperty] private string _merchandiseIncomeText = string.Empty;
        [ObservableProperty] private string _staffWagesText = string.Empty;
        [ObservableProperty] private string _stadiumCostsText = string.Empty;
        [ObservableProperty] private string _prizeMoneyText = string.Empty;
        [ObservableProperty] private string _transferIncomeText = string.Empty;
        [ObservableProperty] private string _transferExpenseText = string.Empty;
        [ObservableProperty] private string _otherIncomeText = string.Empty;
        [ObservableProperty] private string _otherExpensesText = string.Empty;
        [ObservableProperty] private string _financialHealthText = string.Empty;

        [ObservableProperty] private string _currentStaffWagesText = string.Empty;
        [ObservableProperty] private string _currentPlayerWagesText = string.Empty;
        [ObservableProperty] private string _currentSponsorIncomeText = string.Empty;
        [ObservableProperty] private string _currentStadiumMaintenanceText = string.Empty;

        [ObservableProperty] private string _seasonEndForecastText = string.Empty;

        [ObservableProperty] private string _membershipCountText = string.Empty;
        [ObservableProperty] private string _membershipFeeText = string.Empty;
        [ObservableProperty] private string _membershipIncomeMonthlyText = string.Empty;
        [ObservableProperty] private string _membershipIncomeYearlyText = string.Empty;

        public ObservableCollection<SponsorOverviewItem> SponsorDeals { get; } = [];

        [ObservableProperty] private bool _hasActiveLoan;
        [ObservableProperty] private string _loanRemainingText = string.Empty;
        [ObservableProperty] private string _loanMonthlyPaymentText = string.Empty;
        [ObservableProperty] private string _loanInterestRateText = string.Empty;
        [ObservableProperty] private string _loanPayoffEstimateText = string.Empty;

        [ObservableProperty] private bool _hasDirectorOfFootball;
        [ObservableProperty] private string _directorOfFootballName = string.Empty;
        [ObservableProperty] private string _directorOfFootballImagePath = string.Empty;
        [ObservableProperty] private int _directorOfFootballSellingNegotiation;
        [ObservableProperty] private int _directorOfFootballCounterOfferNegotiation;
        [ObservableProperty] private int _directorOfFootballAcceptanceFirmness;
        [ObservableProperty] private int _directorOfFootballFinancialManagement;

        public async Task InitializeAsync()
        {
            var team = _session.ManagerTeam;
            var finances = team?.Finances;
            var state = _session.State;
            if (team is null || finances is null)
                return;

            BalanceText = $"{finances.CurrentBalance:N0} €";
            TicketIncomeText = $"{finances.TicketIncome:N0} €";
            SponsorIncomeText = $"{finances.SponsorIncome:N0} €";
            MerchandiseIncomeText = $"{finances.MerchandiseIncome:N0} €";
            StaffWagesText = $"{finances.StaffWages:N0} €";
            StadiumCostsText = $"{finances.StadiumCosts:N0} €";
            PrizeMoneyText = $"{finances.PrizeMoney:N0} €";
            TransferIncomeText = $"{finances.TransferIncome:N0} €";
            TransferExpenseText = $"{finances.TransferExpense:N0} €";
            OtherIncomeText = $"{finances.OtherIncome:N0} €";
            OtherExpensesText = $"{finances.OtherExpenses:N0} €";
            FinancialHealthText = $"{finances.FinancialHealth} / 100";

            MembershipCountText = $"{finances.ClubMembers:N0}";
            MembershipFeeText = $"{finances.MembershipFeePerMember:N0} € / Jahr";
            long annualMembershipIncome = (long)finances.ClubMembers * finances.MembershipFeePerMember;
            MembershipIncomeMonthlyText = $"{annualMembershipIncome / 12:N0} € / Monat";
            MembershipIncomeYearlyText = $"{annualMembershipIncome:N0} € / Jahr";

            CurrentStaffWagesText = $"{team.Employees.Sum(e => e.Salary):N0} € / Jahr";

            var contracts = await _contractRepository.GetByTeamAsync(team.Id);
            var currentDate = state?.CurrentDate ?? DateTime.MinValue;
            double annualPlayerWages = contracts
                .Where(c => c.HolderType == ContractHolderType.Player && c.EndDate > currentDate)
                .Sum(c => c.AnnualSalary);
            CurrentPlayerWagesText = $"{annualPlayerWages:N0} € / Jahr ({annualPlayerWages / 12:N0} € / Monat)";

            var sponsorships = await _sponsorshipRepository.GetByTeamAsync(team.Id);
            var catalog = await _sponsorRepository.GetAllAsync();
            double currentSponsorIncome = sponsorships
                .Select(s => catalog.FirstOrDefault(c => c.Id == s.SponsorId)?.SeasonPayment ?? 0)
                .Sum();
            CurrentSponsorIncomeText = $"{currentSponsorIncome:N0} € / Saison";

            SponsorDeals.Clear();
            foreach (var deal in sponsorships)
            {
                var sponsor = catalog.FirstOrDefault(c => c.Id == deal.SponsorId);
                if (sponsor is null)
                    continue;

                int expiresAfterSeason = deal.StartSeason + deal.Duration - 1;
                SponsorDeals.Add(new SponsorOverviewItem
                {
                    SlotLabel = SlotLabel(sponsor.SponsorType),
                    SponsorName = sponsor.Name,
                    SeasonPaymentText = $"{sponsor.SeasonPayment:N0} € / Saison",
                    PaymentPerMonthText = $"{sponsor.PaymentPerMonth:N0} € / Monat (bis zu {FinanceService.SponsorPaymentMonths}× jährlich, jeweils zum 15.)",
                    ExpiresText = $"läuft bis Saison {expiresAfterSeason}",
                    Offers = sponsor.Offers,
                });
            }

            CurrentStadiumMaintenanceText = team.Stadium is null
                ? "–"
                : $"{team.Stadium.MaintenanceCosts:N0} € / Saison";

            HasActiveLoan = team.ActiveLoan is { Status: ClubLoanStatus.Active };
            if (HasActiveLoan)
            {
                var loan = team.ActiveLoan!;
                int monthsLeft = ClubLoanService.EstimateMonthsRemaining(loan);
                LoanRemainingText = $"{loan.RemainingBalance:N0} €";
                LoanMonthlyPaymentText = $"{loan.MonthlyPayment:N0} €";
                LoanInterestRateText = $"{loan.AnnualInterestRatePercent:0.#}% p.a.";
                LoanPayoffEstimateText = state is null
                    ? $"{monthsLeft} Monate"
                    : $"ca. {state.CurrentDate.AddMonths(monthsLeft):MMM yyyy} ({monthsLeft} Monate)";
            }

            if (state is not null)
            {
                int projected = await _financeService.EstimateSeasonEndBalanceAsync(team, state.MatchdayIndex, state.CurrentDate);
                SeasonEndForecastText = $"ca. {projected:N0} €";
            }

            var dof = team.Employees.FirstOrDefault(e => e.EmployeeType == EmployeeType.DirectorOfFootball);
            HasDirectorOfFootball = dof is not null;
            if (dof is not null)
            {
                DirectorOfFootballName = dof.Name;
                DirectorOfFootballImagePath = dof.ImagePath ?? string.Empty;
                DirectorOfFootballSellingNegotiation = dof.SellingNegotiation;
                DirectorOfFootballCounterOfferNegotiation = dof.CounterOfferNegotiation;
                DirectorOfFootballAcceptanceFirmness = dof.AcceptanceFirmness;
                DirectorOfFootballFinancialManagement = dof.FinancialManagement;
            }
        }

        private static string SlotLabel(SponsorType slot) => slot switch
        {
            SponsorType.Main => "Hauptsponsor",
            SponsorType.Perimeter => "Bandenwerbung",
            SponsorType.Kit => "Ausrüster",
            _ => slot.ToString(),
        };

        [RelayCommand]
        private Task Back() => _navigation.GoBackAsync();

        [RelayCommand]
        private Task TakeLoan() => _navigation.GoToAsync("clubloan");

        [RelayCommand]
        private Task OpenStaff() => _navigation.GoToAsync("staff");
    }
}
