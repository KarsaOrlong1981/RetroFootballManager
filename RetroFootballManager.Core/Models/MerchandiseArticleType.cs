namespace RetroFootballManager.Models
{
    // 16 catalog articles - FanTrikot/StarSpielerTrikot are deliberately two separate
    // articles (not one item with a toggle) so the star jersey can carry its own
    // wholesale cost/demand and genuinely outsell the generic one (see MerchandiseCatalog).
    // Winterschal/Wintermuetze/WinterHandschue/Sommerkappe/Sommerfahne/T_Shirt are
    // season-specific (see MerchandiseCatalog.Get(type).Season and
    // MerchandiseSalesCalculator.SeasonalFactor).
    public enum MerchandiseArticleType
    {
        FanTrikot,
        StarSpielerTrikot,
        Tasse,
        Poster,
        Schluesselanhaenger,
        Wimpel,
        Winterschal,
        Wintermuetze,
        Sommerkappe,
        Sommerfahne,
        FanSchal,
        FanMuetze,
        WinterHandschue,
        T_Shirt,
        TrainingsAnzug,
        AufKleber
    }

    // Drives MerchandiseSalesCalculator.SeasonalFactor - AllYear articles are unaffected by
    // the current month.
    public enum MerchandiseSeason
    {
        AllYear,
        Summer,
        Winter,
    }
}
