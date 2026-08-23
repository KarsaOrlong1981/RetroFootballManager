namespace RetroFootballManager.Models
{
    // Performance bonus line on a Contract (see ContractBonus). Cup bonuses mirror
    // CompetitionType (minus Friendly, which never pays a bonus).
    public enum ContractBonusType
    {
        Goal,
        Appearance,
        StartingEleven,
        CleanSheet,             // goalkeeper-only
        ChampionshipOrPromotion,
        GermanCupWin,
        ChampionsLeagueWin,
        EuropaCupWin,
    }
}
