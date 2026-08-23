namespace RetroFootballManager.Models
{
    public static class ContractBonusTypeDisplay
    {
        public static string Label(ContractBonusType type) => type switch
        {
            ContractBonusType.Goal => "Torprämie",
            ContractBonusType.Appearance => "Einsatzprämie",
            ContractBonusType.StartingEleven => "Auflaufprämie",
            ContractBonusType.CleanSheet => "Zu-Null-Prämie",
            ContractBonusType.ChampionshipOrPromotion => "Meisterschaft/Aufstieg",
            ContractBonusType.GermanCupWin => "Pokalsieg (Deutscher Pokal)",
            ContractBonusType.ChampionsLeagueWin => "Europapokal der Meister - Sieg",
            ContractBonusType.EuropaCupWin => "Europapokal - Sieg",
            _ => type.ToString(),
        };
    }
}
