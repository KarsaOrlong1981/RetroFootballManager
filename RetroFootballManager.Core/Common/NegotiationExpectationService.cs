using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Manager-phase (fee) side of a negotiation: how far a club's secret fee expectation
    // sits above the listing's plain AskingPrice, and how an offer's fee ratio maps to the
    // 5-tier NegotiationMoodLevel shown in PlayerNegotiationsDialog. Deliberately separate
    // from PlayerValuationService.EstimateMarketValue - the premium here only shapes the
    // negotiation dialog's internal expectation, not the market value used elsewhere
    // (transfer listings, AI valuation) to avoid regressions in unrelated systems.
    public static class NegotiationExpectationService
    {
        public const double MaxPerformancePremium = 0.30;

        // A great young talent already proving it on the pitch is worth holding out for -
        // up to +30% on top of the plain asking price/market value.
        public static double EstimatePerformancePremium(Player player, PlayerStats? seasonStats)
        {
            double premium = 0;

            if (player.Talent >= 85 && player.Age <= 23)
                premium += 0.15;
            else if (player.Talent >= 75 && player.Age <= 25)
                premium += 0.08;

            if (seasonStats is not null && seasonStats.Appearances >= 15 && seasonStats.Rating <= 3.0)
                premium += 0.10;
            else if (seasonStats is not null && seasonStats.Appearances >= 8 && seasonStats.Rating <= 3.3)
                premium += 0.05;

            if (player.Age <= 20)
                premium += 0.05;

            return Math.Min(premium, MaxPerformancePremium);
        }

        public static double EstimateExpectedFee(double baseFee, Player player, PlayerStats? seasonStats) =>
            Math.Round(baseFee * (1 + EstimatePerformancePremium(player, seasonStats)));

        // ratio = offered fee / secret expectation. Centered at 1.0 (offer exactly matches
        // expectation). Reaching Furious ends the negotiation immediately - no extra buffer.
        public static NegotiationMoodLevel EvaluateFeeMood(double offerRatio) => offerRatio switch
        {
            >= 1.15 => NegotiationMoodLevel.Delighted,
            >= 1.0 => NegotiationMoodLevel.Happy,
            >= 0.85 => NegotiationMoodLevel.Neutral,
            >= 0.7 => NegotiationMoodLevel.Impatient,
            _ => NegotiationMoodLevel.Furious,
        };
    }
}
