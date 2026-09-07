using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public record MerchandiseRecommendation(MerchandiseArticleType Type, int RecommendedMin, int RecommendedMax, bool IsExact);

    // Director of Football's purchase-quantity advice - reuses the exact same demand formula
    // the actual weekly sales tick uses (MerchandiseSalesCalculator.CalculateRawDemand), so
    // the recommendation is genuinely "what the shop needs" rather than a separate guess.
    // Accuracy scales with Employee.FinancialManagement (0-100), same noise-scaling pattern
    // as FinanceEstimator - a top-tier director (>=90) gets an exact figure, a weaker one only
    // a wide band. No director at all = no recommendation (caller gates on HasDirectorOfFootball).
    public static class MerchandiseAdvisor
    {
        private const int TopAdvisorThreshold = 90;
        private const double MaxNoiseFraction = 0.6;

        // How many weeks of stock the director aims to keep on hand when recommending a
        // purchase - matches the "3-5 Wochen Vorrat" guidance the department itself follows.
        private const double TargetWeeksOfStock = 4;

        public static MerchandiseRecommendation Recommend(
            MerchandiseArticleStock article, int clubMembers, double performanceFactor, DateTime currentDate,
            double? bestPlayerRating, int financialManagement)
        {
            double weeklyDemand = MerchandiseSalesCalculator.CalculateRawDemand(
                article.Type, article.SellPrice, clubMembers, performanceFactor, currentDate, bestPlayerRating);

            int idealTarget = (int)Math.Round(weeklyDemand * TargetWeeksOfStock);
            int idealPurchase = Math.Max(0, idealTarget - article.Stock);

            if (financialManagement >= TopAdvisorThreshold)
                return new MerchandiseRecommendation(article.Type, idealPurchase, idealPurchase, IsExact: true);

            double noiseFactor = (100 - financialManagement) / 100.0 * MaxNoiseFraction;
            int min = Math.Max(0, (int)Math.Round(idealPurchase * (1 - noiseFactor)));
            int max = (int)Math.Round(idealPurchase * (1 + noiseFactor));
            return new MerchandiseRecommendation(article.Type, min, max, IsExact: false);
        }
    }
}
