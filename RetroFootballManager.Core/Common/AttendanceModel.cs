using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public record AttendanceResult(int SeatingSold, int StandingSold, int LogeSold, int TotalAttendance, double AvgFillRate);

    public static class AttendanceModel
    {
        private const double MinDemand = 0.15;
        private const double MaxDemand = 1.0;

        public static AttendanceResult Calculate(
            Stadium stadium,
            double recentFormPoints0to15,
            int leaguePosition,
            int leagueSize,
            int opponentTierRank,
            double baselinePrice)
        {
            double demand = 0.5;

            demand += 0.20 * ((recentFormPoints0to15 / 15.0) - 0.5) * 2;

            if (leagueSize > 1)
                demand += 0.15 * (1 - ((leaguePosition - 1) / (double)(leagueSize - 1)) - 0.5) * 2;

            demand += 0.10 * (opponentTierRank <= 2 ? 1 : opponentTierRank == 3 ? 0 : -1);

            demand += 0.15 * (stadium.ComfortLevel - 3) / 2.0;

            // Convex price-demand curve instead of linear: small increases barely cost any
            // attendance, but the decline gets noticeably steeper per additional euro beyond
            // that (non-proportional). A price below baseline still behaves linearly/moderately.
            if (baselinePrice > 0)
            {
                double relativeIncrease = Math.Max(0, (stadium.SeatPrice - baselinePrice) / baselinePrice);
                double relativeDecrease = Math.Max(0, (baselinePrice - stadium.SeatPrice) / baselinePrice);
                demand -= 0.35 * Math.Pow(relativeIncrease, 2.2);
                demand += 0.15 * relativeDecrease;
            }

            if (stadium.HasRoof)
                demand += 0.03;

            demand = Math.Clamp(demand, MinDemand, MaxDemand);

            int seating = (int)(stadium.SeatingCapacity * demand);
            int standing = (int)(stadium.StandingCapacity * Math.Clamp(demand * 1.1, MinDemand, MaxDemand));
            int loge = (int)(stadium.LogeCapacity * Math.Clamp(demand * 0.9, MinDemand, MaxDemand));

            int total = seating + standing + loge;
            double avgFillRate = stadium.Capacity > 0 ? (double)total / stadium.Capacity : 0;

            return new AttendanceResult(seating, standing, loge, total, avgFillRate);
        }
    }
}
