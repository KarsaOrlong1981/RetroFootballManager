using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // BaseWeeklyDemandRate: expected fraction of ClubMembers buying this article per week
    // at neutral (1.0) performance/price/season factors - see MerchandiseSalesCalculator.
    public record MerchandiseArticleDefinition(
        MerchandiseArticleType Type, string DisplayName, int WholesaleCost, int DefaultSellPrice,
        double BaseWeeklyDemandRate, MerchandiseSeason Season);

    public static class MerchandiseCatalog
    {
        public static readonly IReadOnlyList<MerchandiseArticleDefinition> Articles =
        [
            new(MerchandiseArticleType.FanTrikot, "Fan-Trikot", 40, 75, 0.0015, MerchandiseSeason.AllYear),
            new(MerchandiseArticleType.StarSpielerTrikot, "Star-Trikot", 40, 85, 0.0012, MerchandiseSeason.AllYear),
            new(MerchandiseArticleType.Tasse, "Tasse", 4, 9, 0.0040, MerchandiseSeason.AllYear),
            new(MerchandiseArticleType.Poster, "Poster", 2, 5, 0.0050, MerchandiseSeason.AllYear),
            new(MerchandiseArticleType.Schluesselanhaenger, "Schlüsselanhänger", 3, 7, 0.0060, MerchandiseSeason.AllYear),
            new(MerchandiseArticleType.Wimpel, "Wimpel", 6, 12, 0.0020, MerchandiseSeason.AllYear),
            new(MerchandiseArticleType.Winterschal, "Winterschal", 8, 15, 0.0030, MerchandiseSeason.Winter),
            new(MerchandiseArticleType.Wintermuetze, "Wintermütze", 10, 18, 0.0025, MerchandiseSeason.Winter),
            new(MerchandiseArticleType.Sommerkappe, "Sommerkappe", 7, 14, 0.0025, MerchandiseSeason.Summer),
            new(MerchandiseArticleType.Sommerfahne, "Sommer-Autofahne", 5, 11, 0.0020, MerchandiseSeason.Summer),
            new(MerchandiseArticleType.FanSchal, "Fan-Schal", 7, 14, 0.0028, MerchandiseSeason.AllYear),
            new(MerchandiseArticleType.FanMuetze, "Fan-Mütze", 9, 16, 0.0022, MerchandiseSeason.AllYear),
            new(MerchandiseArticleType.WinterHandschue, "Winterhandschuhe", 6, 13, 0.0018, MerchandiseSeason.Winter),
            new(MerchandiseArticleType.T_Shirt, "T-Shirt", 12, 25, 0.0035, MerchandiseSeason.Summer),
            new(MerchandiseArticleType.TrainingsAnzug, "Trainingsanzug", 35, 65, 0.0008, MerchandiseSeason.AllYear),
            new(MerchandiseArticleType.AufKleber, "Aufkleber", 1, 3, 0.0080, MerchandiseSeason.AllYear),
        ];

        public static MerchandiseArticleDefinition Get(MerchandiseArticleType type) =>
            Articles.First(a => a.Type == type);
    }
}
