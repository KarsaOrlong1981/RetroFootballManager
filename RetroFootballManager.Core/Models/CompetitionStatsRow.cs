namespace RetroFootballManager.Models
{
    // A career summary row for one non-league competition (a cup type or friendlies) - built
    // from PlayerStats rows scoped to that Competition value, kept separate from the main
    // league season/career totals. See SaveGameService.GetPlayerCompetitionBreakdownAsync.
    public record CompetitionStatsRow(string Label, int Matches, int Goals, int Assists, int YellowCards, int RedCards);
}
