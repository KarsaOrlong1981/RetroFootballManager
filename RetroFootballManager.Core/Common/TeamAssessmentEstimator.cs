using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Estimated rating/morale/finances of another club, as shown in the club overview -
    // depends entirely on having an Analyst on staff (Employee.TeamAssessment). Without one,
    // EVERY field is null (shown as "?" in the UI) - not even a rough traffic-light value,
    // per explicit user request. Own team is always exact regardless (see ClubViewModel).
    public record TeamAssessmentEstimate(
        double? Rating, int? Morale, double? Balance, double? TransferBudget, int? FinancialHealth,
        bool IsExact, string AccuracyLabel)
    {
        public static readonly TeamAssessmentEstimate Unknown = new(null, null, null, null, null, false, "Unbekannt");
    }

    // Modeled on ScoutingService.GetRecommendations' noise pattern: deterministic per
    // (team, season, month) via the caller-supplied Random (see HashCode.Combine usage there),
    // so re-opening the same team's detail view within the same month doesn't show a
    // different estimate each time.
    public static class TeamAssessmentEstimator
    {
        // TeamAssessment at/above this gives an exact reading instead of a noisy estimate.
        private const int TopAnalystThreshold = 90;

        // Max noise fraction/spread (+/-) applied at TeamAssessment = 1; scales down to 0 at 100.
        private const double RatingSpread = 0.25;
        private const double BalanceSpread = 0.35;
        private const double TransferBudgetSpread = 0.35;
        private const int MoraleSpread = 25; // points, not a fraction
        private const int FinancialHealthSpread = 40; // points, not a fraction

        public static TeamAssessmentEstimate Estimate(
            double realRating, int realMorale, Finances? finances, int? teamAssessmentAbility, Random rng)
        {
            if (teamAssessmentAbility is null || finances is null)
                return TeamAssessmentEstimate.Unknown;

            if (teamAssessmentAbility.Value >= TopAnalystThreshold)
                return new TeamAssessmentEstimate(
                    realRating, realMorale, finances.CurrentBalance, finances.TransferBudget, finances.FinancialHealth,
                    IsExact: true, AccuracyLabel: "Exakt");

            double noiseFactor = (100 - teamAssessmentAbility.Value) / 100.0;

            double Noise() => (rng.NextDouble() * 2 - 1) * noiseFactor;

            double estimatedRating = realRating * (1 + Noise() * RatingSpread);
            int estimatedMorale = Math.Clamp(realMorale + (int)Math.Round(Noise() * MoraleSpread), 0, 100);
            double estimatedBalance = finances.CurrentBalance * (1 + Noise() * BalanceSpread);
            double estimatedTransferBudget = finances.TransferBudget * (1 + Noise() * TransferBudgetSpread);
            int estimatedHealth = Math.Clamp(finances.FinancialHealth + (int)Math.Round(Noise() * FinancialHealthSpread), 0, 100);

            string accuracyLabel = teamAssessmentAbility.Value >= 75 ? "Gut geschätzt"
                : teamAssessmentAbility.Value >= 50 ? "Grob geschätzt"
                : "Sehr unsicher";

            return new TeamAssessmentEstimate(
                estimatedRating, estimatedMorale, estimatedBalance, estimatedTransferBudget, estimatedHealth,
                IsExact: false, accuracyLabel);
        }
    }
}
