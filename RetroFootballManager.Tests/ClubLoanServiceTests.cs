using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class ClubLoanServiceTests
    {
        private static readonly DateTime CurrentDate = new(2026, 8, 15);

        private static Team CreateTeam(int leagueTier = 2, int balance = 100_000)
        {
            var team = TestHelpers.CreateTeam("Kredit FC", baseRating: 60);
            team.Id = 1;
            team.LeagueTier = leagueTier;
            team.Finances = new Finances { CurrentBalance = balance };
            return team;
        }

        [Fact]
        public void CalculateMonthlyPayment_MatchesAnnuityFormula_ForKnownInputs()
        {
            // 120,000 @ 6% p.a. over 12 months - textbook annuity result.
            int payment = ClubLoanService.CalculateMonthlyPayment(120_000, 6.0, 12);

            double monthlyRate = 0.06 / 12.0;
            int expected = (int)Math.Round(120_000 * monthlyRate / (1 - Math.Pow(1 + monthlyRate, -12)));
            Assert.Equal(expected, payment);
        }

        [Fact]
        public void TryTakeLoan_RejectsSecondLoan_WhileOneActive()
        {
            var team = CreateTeam();
            Assert.True(ClubLoanService.TryTakeLoan(team, 100_000, 12, CurrentDate, out _));

            bool ok = ClubLoanService.TryTakeLoan(team, 50_000, 12, CurrentDate, out string? error);

            Assert.False(ok);
            Assert.NotNull(error);
        }

        [Fact]
        public void TryTakeLoan_RejectsAmountAboveTierMax()
        {
            var team = CreateTeam(leagueTier: 4); // max 250,000

            bool ok = ClubLoanService.TryTakeLoan(team, 300_000, 12, CurrentDate, out string? error);

            Assert.False(ok);
            Assert.NotNull(error);
        }

        [Fact]
        public void TryTakeLoan_CreditsPrincipalToCurrentBalance_OnSuccess()
        {
            var team = CreateTeam(balance: 10_000);

            bool ok = ClubLoanService.TryTakeLoan(team, 100_000, 12, CurrentDate, out string? error);

            Assert.True(ok);
            Assert.Null(error);
            Assert.Equal(110_000, team.Finances!.CurrentBalance);
            Assert.Equal(ClubLoanStatus.Active, team.ActiveLoan!.Status);
        }

        [Fact]
        public void ApplyMonthlyPayment_DoesNotFire_BeforeThe15th()
        {
            var team = CreateTeam();
            ClubLoanService.TryTakeLoan(team, 100_000, 12, CurrentDate, out _);
            var loan = team.ActiveLoan!;
            int balanceBefore = loan.RemainingBalance;

            var result = ClubLoanService.ApplyMonthlyPayment(loan, new DateTime(2026, 9, 10));

            Assert.Null(result);
            Assert.Equal(balanceBefore, loan.RemainingBalance);
        }

        [Fact]
        public void ApplyMonthlyPayment_FiresOnlyOncePerCalendarMonth()
        {
            var team = CreateTeam();
            ClubLoanService.TryTakeLoan(team, 100_000, 12, CurrentDate, out _);
            var loan = team.ActiveLoan!;

            var first = ClubLoanService.ApplyMonthlyPayment(loan, new DateTime(2026, 9, 15));
            var second = ClubLoanService.ApplyMonthlyPayment(loan, new DateTime(2026, 9, 20));

            Assert.NotNull(first);
            Assert.Null(second);
        }

        [Fact]
        public void ApplyMonthlyPayment_ReducesRemainingBalance_ByPrincipalPortion()
        {
            var team = CreateTeam();
            ClubLoanService.TryTakeLoan(team, 100_000, 12, CurrentDate, out _);
            var loan = team.ActiveLoan!;
            int balanceBefore = loan.RemainingBalance;

            var result = ClubLoanService.ApplyMonthlyPayment(loan, new DateTime(2026, 9, 15));

            Assert.NotNull(result);
            Assert.Equal(balanceBefore - result!.Value.PrincipalPortion, loan.RemainingBalance);
        }

        [Fact]
        public void ApplyMonthlyPayment_MarksPaidOff_WhenFinalInstallmentClearsBalance()
        {
            var team = CreateTeam();
            ClubLoanService.TryTakeLoan(team, 100_000, 12, CurrentDate, out _);
            var loan = team.ActiveLoan!;

            var date = CurrentDate;
            for (int i = 0; i < loan.TermMonths; i++)
            {
                date = date.AddMonths(1);
                ClubLoanService.ApplyMonthlyPayment(loan, date);
            }

            Assert.Equal(0, loan.RemainingBalance);
            Assert.Equal(ClubLoanStatus.PaidOff, loan.Status);
        }

        [Fact]
        public void EstimateMonthsRemaining_MatchesRemainingBalanceDividedByPayment()
        {
            var team = CreateTeam();
            ClubLoanService.TryTakeLoan(team, 100_000, 12, CurrentDate, out _);
            var loan = team.ActiveLoan!;

            int expected = (int)Math.Ceiling(loan.RemainingBalance / (double)loan.MonthlyPayment);
            Assert.Equal(expected, ClubLoanService.EstimateMonthsRemaining(loan));
        }

        [Fact]
        public void TryPayOffEarly_ClearsBalance_WhenAffordable()
        {
            var team = CreateTeam(balance: 200_000);
            ClubLoanService.TryTakeLoan(team, 100_000, 12, CurrentDate, out _);
            var loan = team.ActiveLoan!;
            int balanceBeforePayoff = team.Finances!.CurrentBalance;
            int remainingBeforePayoff = loan.RemainingBalance;

            bool ok = ClubLoanService.TryPayOffEarly(team, out string? error);

            Assert.True(ok);
            Assert.Null(error);
            Assert.Equal(ClubLoanStatus.PaidOff, loan.Status);
            Assert.Equal(0, loan.RemainingBalance);
            Assert.Equal(balanceBeforePayoff - remainingBeforePayoff, team.Finances.CurrentBalance);
        }
    }
}
