using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Estimated finances of another club, as shown to the manager in the club overview.
    // EstimatedBalance/EstimatedTransferBudget are null when there's no analyst on staff -
    // FinancialHealth (a rough traffic-light value) is always visible regardless.
    public record FinanceEstimate(
        double? EstimatedBalance,
        double? EstimatedTransferBudget,
        int FinancialHealth,
        bool IsExact,
        string AccuracyLabel);

    // Modeled on ScoutingService.GetRecommendations' noise pattern: deterministic per
    // (team, season, month) via the caller-supplied Random (see HashCode.Combine usage there),
    // so re-opening the same team's detail view within the same month doesn't show a
    // different estimate each time.
    public static class FinanceEstimator
    {
        // AnalysisAbility at/above this gives an exact reading instead of a noisy estimate.
        private const int TopAnalystThreshold = 90;

        // Max noise fraction (+/-) applied at AnalysisAbility = 1; scales down to 0 at 100.
        private const double BalanceSpread = 0.35;
        private const double TransferBudgetSpread = 0.35;

        public static FinanceEstimate Estimate(Finances real, int? analysisAbility, Random rng)
        {
            if (analysisAbility is null)
                return new FinanceEstimate(null, null, real.FinancialHealth, IsExact: false, AccuracyLabel: "Unbekannt");

            if (analysisAbility.Value >= TopAnalystThreshold)
                return new FinanceEstimate(
                    real.CurrentBalance, real.TransferBudget, real.FinancialHealth, IsExact: true, AccuracyLabel: "Exakt");

            double noiseFactor = (100 - analysisAbility.Value) / 100.0;
            double balanceNoise = (rng.NextDouble() * 2 - 1) * noiseFactor * BalanceSpread;
            double transferNoise = (rng.NextDouble() * 2 - 1) * noiseFactor * TransferBudgetSpread;

            double estimatedBalance = real.CurrentBalance * (1 + balanceNoise);
            double estimatedTransferBudget = real.TransferBudget * (1 + transferNoise);

            string accuracyLabel = analysisAbility.Value >= 75 ? "Gut geschätzt"
                : analysisAbility.Value >= 50 ? "Grob geschätzt"
                : "Sehr unsicher";

            return new FinanceEstimate(estimatedBalance, estimatedTransferBudget, real.FinancialHealth, IsExact: false, accuracyLabel);
        }
    }
}
