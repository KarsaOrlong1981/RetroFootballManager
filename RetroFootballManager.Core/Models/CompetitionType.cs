namespace RetroFootballManager.Models
{
    public enum CompetitionType
    {
        GermanCup,
        ChampionsLeague,
        EuropaCup,

        // Friendlies - PlayerStats scoping only (see MatchDayService.PersistPlayerStatsAsync /
        // ApplyCareerMinutes). Never used for trophies, suspensions, or cup-tie logic - a
        // friendly never has a CupTie row and never serves/starts a suspension.
        Friendly,
    }
}
