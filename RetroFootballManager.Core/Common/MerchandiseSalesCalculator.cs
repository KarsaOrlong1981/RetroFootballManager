using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public record MerchandiseSaleResult(MerchandiseArticleType Type, int UnitsSold, int Revenue);

    // Pure weekly-sales math - no DB/side effects (mirrors AttendanceModel). Demand for an
    // article scales with the fanbase (ClubMembers), how well the season is going
    // (PerformanceFactor: form + table position), and how the chosen sell price compares to
    // the catalog's reference markup (cheaper than the reference sells faster, pricier sells
    // slower). The star-player jersey additionally scales with the current best player's
    // Rating, so it genuinely outsells the generic jersey once a real star is on the books.
    public static class MerchandiseSalesCalculator
    {
        // Rating at which the star jersey's demand multiplier is exactly neutral (1.0) -
        // an average player's face doesn't move more shirts than the generic jersey.
        private const double StarRatingBaseline = 50.0;

        public static double PerformanceFactor(StandingRow? row, int leagueSize)
        {
            double formPoints = FinanceService.FormPoints(row?.Form ?? string.Empty);
            double positionFactor = row is null || leagueSize <= 0
                ? 1.0
                : (leagueSize - row.Position + 1.0) / leagueSize;

            return Math.Clamp(0.6 + 0.5 * (formPoints / 15.0) + 0.3 * positionFactor, 0.5, 1.8);
        }

        // Winter articles sell 80% better Nov-Feb and 50% worse Jun-Aug (and mirrored for
        // summer articles) - AllYear articles are always 1.0.
        private const double InSeasonBoost = 1.8;
        private const double OffSeasonPenalty = 0.5;

        public static double SeasonalFactor(MerchandiseSeason season, DateTime currentDate)
        {
            if (season == MerchandiseSeason.AllYear)
                return 1.0;

            bool isWinterMonth = currentDate.Month is 11 or 12 or 1 or 2;
            bool isSummerMonth = currentDate.Month is 6 or 7 or 8;

            if (season == MerchandiseSeason.Winter)
                return isWinterMonth ? InSeasonBoost : isSummerMonth ? OffSeasonPenalty : 1.0;

            return isSummerMonth ? InSeasonBoost : isWinterMonth ? OffSeasonPenalty : 1.0;
        }

        // Raw weekly demand, WITHOUT capping to current stock - the shared core formula.
        // CalculateWeeklySale (actual sales) caps this to available stock; MerchandiseAdvisor
        // (Director of Football purchase recommendation) uses the uncapped figure directly,
        // since a recommendation needs "how much would sell" independent of what's on hand.
        public static double CalculateRawDemand(
            MerchandiseArticleType type, int sellPrice, int clubMembers, double performanceFactor, DateTime currentDate,
            double? bestPlayerRating = null)
        {
            var def = MerchandiseCatalog.Get(type);

            double referenceMarkup = def.DefaultSellPrice / (double)def.WholesaleCost;
            double actualMarkup = sellPrice / (double)Math.Max(def.WholesaleCost, 1);
            double priceFactor = Math.Clamp(referenceMarkup / Math.Max(actualMarkup, 0.1), 0.3, 2.0);

            double ratingFactor = type == MerchandiseArticleType.StarSpielerTrikot
                ? Math.Clamp((bestPlayerRating ?? StarRatingBaseline) / StarRatingBaseline, 0.5, 3.0)
                : 1.0;

            double seasonalFactor = SeasonalFactor(def.Season, currentDate);

            return clubMembers * def.BaseWeeklyDemandRate * performanceFactor * priceFactor * ratingFactor * seasonalFactor;
        }

        public static MerchandiseSaleResult CalculateWeeklySale(
            MerchandiseArticleStock article, int clubMembers, double performanceFactor, DateTime currentDate,
            double? bestPlayerRating = null)
        {
            double demand = CalculateRawDemand(article.Type, article.SellPrice, clubMembers, performanceFactor, currentDate, bestPlayerRating);
            int unitsSold = Math.Min(article.Stock, (int)Math.Round(demand));
            return new MerchandiseSaleResult(article.Type, unitsSold, unitsSold * article.SellPrice);
        }
    }
}
