using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public record AttendanceResult(int SeatingSold, int StandingSold, int LogeSold, int TotalAttendance, double AvgFillRate);

    public static class AttendanceModel
    {
        private const double MinDemand = 0.15;
        private const double MaxDemand = 1.0;

        // Standing is traditionally cheaper than seating, loge/VIP much pricier - these ratios
        // turn the single seat-tier baselinePrice into a baseline for the other two tiers too.
        private const double StandingBaselineRatio = 0.5;
        private const double LogeBaselineRatio = 4.0;

        public static AttendanceResult Calculate(
            Stadium stadium,
            double recentFormPoints0to15,
            int leaguePosition,
            int leagueSize,
            int opponentTierRank,
            double baselinePrice)
        {
            double baseDemand = 0.5;

            baseDemand += 0.20 * ((recentFormPoints0to15 / 15.0) - 0.5) * 2;

            if (leagueSize > 1)
                baseDemand += 0.15 * (1 - ((leaguePosition - 1) / (double)(leagueSize - 1)) - 0.5) * 2;

            baseDemand += 0.10 * (opponentTierRank <= 2 ? 1 : opponentTierRank == 3 ? 0 : -1);

            baseDemand += 0.15 * (stadium.ComfortLevel - 3) / 2.0;

            if (stadium.HasRoof)
                baseDemand += 0.03;

            // Each tier reacts to its OWN price against its own tier-scaled baseline - previously
            // only SeatPrice affected demand at all, so changing StandingPrice/LogePrice moved
            // revenue per ticket but never the number of tickets sold.
            double seatDemand = Math.Clamp(
                ApplyPriceElasticity(baseDemand, stadium.SeatPrice, baselinePrice), MinDemand, MaxDemand);
            double standingDemand = Math.Clamp(
                ApplyPriceElasticity(baseDemand, stadium.StandingPrice, baselinePrice * StandingBaselineRatio) * 1.1, MinDemand, MaxDemand);
            double logeDemand = Math.Clamp(
                ApplyPriceElasticity(baseDemand, stadium.LogePrice, baselinePrice * LogeBaselineRatio) * 0.9, MinDemand, MaxDemand);

            int seating = (int)(stadium.SeatingCapacity * seatDemand);
            int standing = (int)(stadium.StandingCapacity * standingDemand);
            int loge = (int)(stadium.LogeCapacity * logeDemand);

            int total = seating + standing + loge;
            double avgFillRate = stadium.Capacity > 0 ? (double)total / stadium.Capacity : 0;

            return new AttendanceResult(seating, standing, loge, total, avgFillRate);
        }

        // Convenience for pre-match displays (match day pages) - mirrors FinanceService.
        // CalculateTicketIncome's inputs exactly (same formula, just callable before the
        // match/finance booking happens), so the shown estimate matches what actually gets
        // booked for a real league home game. homeLeagueStandings must be the home team's
        // OWN league's table (its own form/position drive its own crowd demand).
        public static AttendanceResult EstimateForFixture(
            Stadium stadium,
            int homeTeamId,
            int homeLeagueTier,
            int awayLeagueTier,
            IReadOnlyList<StandingRow> homeLeagueStandings)
        {
            var row = homeLeagueStandings.FirstOrDefault(s => s.TeamId == homeTeamId);
            double formPoints = FinanceService.FormPoints(row?.Form ?? string.Empty);
            int leaguePosition = row?.Position ?? homeLeagueStandings.Count;
            double baselinePrice = 10 + (4 - homeLeagueTier) * 8; // matches UniverseGenerator's tier pricing

            return Calculate(
                stadium, formPoints, leaguePosition, Math.Max(homeLeagueStandings.Count, 1), awayLeagueTier, baselinePrice);
        }

        // Convex price-demand curve instead of linear: small increases barely cost any
        // attendance, but the decline gets noticeably steeper per additional euro beyond
        // that (non-proportional). A price below baseline still behaves linearly/moderately.
        private static double ApplyPriceElasticity(double demand, double price, double baselinePrice)
        {
            if (baselinePrice <= 0)
                return demand;

            double relativeIncrease = Math.Max(0, (price - baselinePrice) / baselinePrice);
            double relativeDecrease = Math.Max(0, (baselinePrice - price) / baselinePrice);
            return demand - 0.35 * Math.Pow(relativeIncrease, 2.2) + 0.15 * relativeDecrease;
        }
    }
}
