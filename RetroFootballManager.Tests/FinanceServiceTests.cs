using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class FinanceServiceTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private SponsorRepository _sponsorRepo = null!;
        private SponsorshipRepository _sponsorshipRepo = null!;
        private ContractRepository _contractRepo = null!;
        private FinanceService _service = null!;

        private static readonly DateTime CurrentDate = new(2026, 8, 1);

        public FinanceServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_finance_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _sponsorRepo = new SponsorRepository(_db);
            _sponsorshipRepo = new SponsorshipRepository(_db);
            _contractRepo = new ContractRepository(_db);
            _service = new FinanceService(_sponsorRepo, _sponsorshipRepo, _contractRepo);
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        private static Team CreateTeamWithEconomy()
        {
            var team = TestHelpers.CreateTeam("Finanz FC", baseRating: 60);
            team.Id = 1;
            team.LeagueTier = 2;
            team.Stadium = new Stadium
            {
                SeatingCapacity = 20_000, StandingCapacity = 5_000, LogeCapacity = 200,
                SeatPrice = 20, StandingPrice = 10, LogePrice = 80,
                ComfortLevel = 3, MaintenanceCosts = 340_000,
            };
            team.Finances = new Finances { CurrentBalance = 100_000 };
            return team;
        }

        private static readonly List<StandingRow> Standings =
        [
            new(1, 1, "Finanz FC", 5, 3, 1, 1, 10, 5, 5, 10, "WWDWL"),
        ];

        [Fact]
        public void ApplyMatchdayFinance_HomeFixture_CreditsTicketIncome()
        {
            var team = CreateTeamWithEconomy();
            var result = _service.ApplyMatchdayFinance(team, isHome: true, Standings, opponentTierRank: 2);

            Assert.True(result.TicketIncome > 0);
            Assert.Equal(result.TicketIncome, team.Finances!.TicketIncome);
        }

        [Fact]
        public void ApplyMatchdayFinance_AwayFixture_NoTicketIncome()
        {
            var team = CreateTeamWithEconomy();
            var result = _service.ApplyMatchdayFinance(team, isHome: false, Standings, opponentTierRank: 2);

            Assert.Equal(0, result.TicketIncome);
        }

        [Fact]
        public void ApplyMatchdayFinance_NeverPaysSponsorBonus_PaidOnlyAtSeasonEnd()
        {
            // Win bonuses are tallied over the season and paid as one lump sum at season end
            // (SaveGameService.PaySponsorSeasonBonusesAsync) - matchday finance must not touch
            // SponsorIncome at all.
            var team = CreateTeamWithEconomy();
            _service.ApplyMatchdayFinance(team, isHome: false, Standings, opponentTierRank: 2);

            Assert.Equal(0, team.Finances!.SponsorIncome);
        }

        [Fact]
        public async Task ApplyMonthlySettlement_DoesNotFire_BeforeThe15th()
        {
            var team = CreateTeamWithEconomy();
            team.Employees.Add(new Employee { Salary = 12_000 });

            bool applied = await _service.ApplyMonthlySettlementAsync(team, new DateTime(2026, 8, 10));

            Assert.False(applied);
            Assert.Equal(0, team.Finances!.StaffWages);
        }

        [Fact]
        public async Task ApplyMonthlySettlement_DeductsStaffWages_AsAnnualDividedByTwelve()
        {
            var team = CreateTeamWithEconomy();
            team.Employees.Add(new Employee { Salary = 12 * 100 });

            bool applied = await _service.ApplyMonthlySettlementAsync(team, new DateTime(2026, 8, 15));

            Assert.True(applied);
            Assert.Equal(100, team.Finances!.StaffWages);
        }

        [Fact]
        public async Task ApplyMonthlySettlement_DeductsStadiumCosts_AsAnnualDividedByTwelve()
        {
            var team = CreateTeamWithEconomy(); // MaintenanceCosts = 340_000

            await _service.ApplyMonthlySettlementAsync(team, new DateTime(2026, 8, 15));

            Assert.Equal((int)Math.Round(340_000 / 12.0), team.Finances!.StadiumCosts);
        }

        [Fact]
        public async Task ApplyMonthlySettlement_DeductsActivePlayerWages_IgnoresExpiredContracts()
        {
            var team = CreateTeamWithEconomy();
            await _contractRepo.SaveAsync(new Contract
            {
                HolderId = 1, HolderType = ContractHolderType.Player, TeamId = team.Id,
                StartDate = CurrentDate.AddYears(-1), EndDate = CurrentDate.AddYears(1),
                AnnualSalary = 12 * 200,
            });
            await _contractRepo.SaveAsync(new Contract
            {
                HolderId = 2, HolderType = ContractHolderType.Player, TeamId = team.Id,
                StartDate = CurrentDate.AddYears(-3), EndDate = CurrentDate.AddYears(-1), // abgelaufen
                AnnualSalary = 12 * 999,
            });

            await _service.ApplyMonthlySettlementAsync(team, new DateTime(2026, 8, 15));

            Assert.Equal(200, team.Finances!.PlayerWages);
        }

        [Fact]
        public async Task ApplyMonthlySettlement_FiresOnlyOncePerCalendarMonth()
        {
            var team = CreateTeamWithEconomy();
            team.Employees.Add(new Employee { Salary = 12 * 100 });

            bool first = await _service.ApplyMonthlySettlementAsync(team, new DateTime(2026, 8, 15));
            bool second = await _service.ApplyMonthlySettlementAsync(team, new DateTime(2026, 8, 20));
            bool nextMonth = await _service.ApplyMonthlySettlementAsync(team, new DateTime(2026, 9, 15));

            Assert.True(first);
            Assert.False(second);
            Assert.True(nextMonth);
            Assert.Equal(200, team.Finances!.StaffWages); // 100 einmal im August + 100 einmal im September
        }

        [Fact]
        public async Task ApplyMonthlySettlement_DeductsLoanPayment_FromCurrentBalance()
        {
            var withLoan = CreateTeamWithEconomy();
            Assert.True(ClubLoanService.TryTakeLoan(withLoan, 100_000, 12, CurrentDate, out _));
            var loan = withLoan.ActiveLoan!;
            int expectedInterest = (int)Math.Round(loan.RemainingBalance * (loan.AnnualInterestRatePercent / 100.0 / 12.0));
            int expectedPrincipal = loan.MonthlyPayment - expectedInterest;

            var withoutLoan = CreateTeamWithEconomy();
            withoutLoan.Finances!.CurrentBalance += 100_000; // match the loan payout, isolate the loan-payment effect

            await _service.ApplyMonthlySettlementAsync(withLoan, new DateTime(2026, 8, 15));
            await _service.ApplyMonthlySettlementAsync(withoutLoan, new DateTime(2026, 8, 15));

            Assert.Equal(withoutLoan.Finances.CurrentBalance - (expectedInterest + expectedPrincipal), withLoan.Finances!.CurrentBalance);
            Assert.Equal(loan.PrincipalAmount - expectedPrincipal, loan.RemainingBalance);
        }

        [Fact]
        public async Task ApplyMonthlySettlement_NoActiveLoan_DoesNotAffectBalance()
        {
            var team = CreateTeamWithEconomy();
            int balanceBefore = team.Finances!.CurrentBalance;

            await _service.ApplyMonthlySettlementAsync(team, new DateTime(2026, 8, 15));

            Assert.Null(team.ActiveLoan);
            int expectedNet = team.Finances.SponsorIncome - team.Finances.StaffWages
                - team.Finances.PlayerWages - team.Finances.StadiumCosts;
            Assert.Equal(balanceBefore + expectedNet, team.Finances.CurrentBalance);
        }

        [Fact]
        public async Task ApplyMonthlySettlement_LoanPayment_AlsoGuardedOncePerMonth()
        {
            var team = CreateTeamWithEconomy();
            ClubLoanService.TryTakeLoan(team, 100_000, 12, CurrentDate, out _);
            var loan = team.ActiveLoan!;

            await _service.ApplyMonthlySettlementAsync(team, new DateTime(2026, 8, 15));
            int remainingAfterFirst = loan.RemainingBalance;
            await _service.ApplyMonthlySettlementAsync(team, new DateTime(2026, 8, 20));

            Assert.Equal(remainingAfterFirst, loan.RemainingBalance);
        }

        [Fact]
        public void EstimateSeasonEndBalance_WithNoLoan_MatchesExistingBehavior()
        {
            var finances = new Finances
            {
                CurrentBalance = 100_000, TicketIncome = 50_000, MerchandiseIncome = 10_000,
                SponsorIncome = 20_000, StaffWages = 5_000, PlayerWages = 15_000, StadiumCosts = 3_000,
            };

            int projected = FinanceService.EstimateSeasonEndBalance(finances, matchdaysPlayed: 10);

            Assert.True(projected != 0);
        }

        [Fact]
        public void EstimateSeasonEndBalance_AtSeasonStart_DoesNotBlowUpFromDivisionByZero()
        {
            var finances = new Finances { CurrentBalance = 100_000 };

            int projected = FinanceService.EstimateSeasonEndBalance(finances, matchdaysPlayed: 0);

            // Nothing booked yet in this fixture, so nothing to project a crisis from - just
            // the current balance carried forward.
            Assert.Equal(100_000, projected);
        }

        [Fact]
        public void EstimateSeasonEndBalance_AtSeasonStart_ExtrapolatesAlreadyBookedPreseasonCosts()
        {
            // One preseason settlement already booked (ApplyMonthlySettlementAsync runs before
            // matchday 1 too) - the projection must react to it immediately instead of waiting
            // for matchday 1, but without amplifying it into a wildly exaggerated forecast.
            var finances = new Finances
            {
                CurrentBalance = 100_000, SponsorIncome = 1_000,
                StaffWages = 2_000, PlayerWages = 10_000, StadiumCosts = 1_000,
            };

            int projected = FinanceService.EstimateSeasonEndBalance(finances, matchdaysPlayed: 0);

            // Net -12,000/month, floored at 1 elapsed month, extrapolated over the 12 remaining.
            Assert.Equal(100_000 - 12_000 * 12, projected);
        }

        [Fact]
        public void EstimateSeasonEndBalance_WithActiveLoan_SubtractsProjectedRemainingPayments()
        {
            var finances = new Finances
            {
                CurrentBalance = 100_000, TicketIncome = 50_000, MerchandiseIncome = 10_000,
                SponsorIncome = 20_000, StaffWages = 5_000, PlayerWages = 15_000, StadiumCosts = 3_000,
            };
            var loan = new ClubLoan
            {
                RemainingBalance = 50_000, MonthlyPayment = 5_000, Status = ClubLoanStatus.Active,
            };

            int withoutLoan = FinanceService.EstimateSeasonEndBalance(finances, matchdaysPlayed: 10);
            int withLoan = FinanceService.EstimateSeasonEndBalance(finances, matchdaysPlayed: 10, loan);

            Assert.True(withLoan < withoutLoan);
        }

        [Fact]
        public async Task EstimateSeasonEndBalanceAsync_AtSeasonStart_ReflectsContractedCosts_EvenBeforeAnySettlement()
        {
            // Brand-new save, matchdaysPlayed=0, no ApplyMonthlySettlementAsync has ever run -
            // nothing is booked into Finances yet, but the contracted staff/player wages and
            // stadium upkeep are already known and must already drag the forecast down.
            var team = CreateTeamWithEconomy(); // Stadium.MaintenanceCosts = 340_000, CurrentBalance = 100_000
            team.Employees.Add(new Employee { Salary = 120_000 });
            await _contractRepo.SaveAsync(new Contract
            {
                HolderId = 1, HolderType = ContractHolderType.Player, TeamId = team.Id,
                StartDate = CurrentDate.AddYears(-1), EndDate = CurrentDate.AddYears(1),
                AnnualSalary = 1_200_000,
            });

            int projected = await _service.EstimateSeasonEndBalanceAsync(team, matchdaysPlayed: 0, CurrentDate);

            Assert.Equal(100_000 - 12 * (100_000 + 10_000 + 28_333), projected);
        }

        [Fact]
        public async Task EstimateSeasonEndBalanceAsync_AtSeasonStart_WithNoCommitments_CarriesBalanceForward()
        {
            var team = CreateTeamWithEconomy();
            team.Stadium!.MaintenanceCosts = 0;

            int projected = await _service.EstimateSeasonEndBalanceAsync(team, matchdaysPlayed: 0, CurrentDate);

            Assert.Equal(100_000, projected);
        }

        [Fact]
        public void RolloverSeason_ZeroesSeasonTrackedFields()
        {
            var finances = new Finances
            {
                TransferIncome = 500, TransferExpense = 200, StadiumCosts = 1000,
                TicketIncome = 5000, SponsorIncome = 2000, MerchandiseIncome = 300, StaffWages = 100,
                PlayerWages = 400, CurrentBalance = 999_999,
            };

            FinanceService.RolloverSeason(finances);

            Assert.Equal(0, finances.TransferIncome);
            Assert.Equal(0, finances.TransferExpense);
            Assert.Equal(0, finances.StadiumCosts);
            Assert.Equal(0, finances.TicketIncome);
            Assert.Equal(0, finances.SponsorIncome);
            Assert.Equal(0, finances.MerchandiseIncome);
            Assert.Equal(0, finances.StaffWages);
            Assert.Equal(0, finances.PlayerWages);
            Assert.Equal(999_999, finances.CurrentBalance);
        }
    }
}
