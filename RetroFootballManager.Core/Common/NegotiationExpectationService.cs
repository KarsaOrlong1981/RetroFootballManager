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

        // Floor for how much of a loan-out's wage the borrowing club is expected to cover -
        // never realistically less than this, regardless of player quality (premiums only push
        // it higher, see EstimateExpectedFee's loan callers).
        public const double BaseExpectedWageSharePercentage = 75.0;

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

        // A club sitting well below the player's level has to dig deep to prise him away from a
        // stronger one - same for pulling him down to their own level on loan. Team.LeagueTier is
        // 1 (top) .. 4 (bottom), so a positive gap means the buyer/borrower plays in a weaker
        // league than the seller/lender. Zero for a lateral or upward move, or for a player who
        // isn't good enough for this to matter in the first place.
        public const int EliteRatingThreshold = 70;
        public const double MaxLevelGapPremiumPerTier = 0.6;

        public static double EstimateLevelGapPremium(Player player, int sellingTeamTier, int buyingTeamTier)
        {
            int tierGap = buyingTeamTier - sellingTeamTier;
            if (tierGap <= 0 || player.Rating < EliteRatingThreshold)
                return 0;

            double qualityFactor = Math.Clamp((player.Rating - EliteRatingThreshold) / 25.0, 0, 1);
            return tierGap * MaxLevelGapPremiumPerTier * qualityFactor;
        }

        public static double EstimateExpectedFee(double baseFee, Player player, PlayerStats? seasonStats) =>
            EstimateExpectedFee(baseFee, player, seasonStats, sellingTeamTier: 0, buyingTeamTier: 0);

        public static double EstimateExpectedFee(
            double baseFee, Player player, PlayerStats? seasonStats, int sellingTeamTier, int buyingTeamTier)
        {
            double premium = EstimatePerformancePremium(player, seasonStats)
                + EstimateLevelGapPremium(player, sellingTeamTier, buyingTeamTier);
            return Math.Round(baseFee * (1 + premium));
        }

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
