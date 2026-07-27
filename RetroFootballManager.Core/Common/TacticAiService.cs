using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // AI counterpart to the pre-match report (M6a ScoutingReportService): the human only sees a
    // tactic suggestion, COM teams apply it automatically before the match - an AI doesn't need
    // an analyst to adapt its own tactics to the opponent.
    public static class TacticAiService
    {
        // How often the AI actually adjusts its tactic before a match (instead of sticking with
        // the last one) - scales with Difficulty.
        private static double ActivityChance(Difficulty difficulty) => difficulty switch
        {
            Difficulty.Easy => 0.3,
            Difficulty.Hard => 0.9,
            _ => 0.6,
        };

        public static bool ApplyPreMatchTactic(
            Team aiTeam, Team opponent, List<StandingRow> leagueStandings, List<Team> leagueTeams,
            Difficulty difficulty, Random rng)
        {
            if (rng.NextDouble() > ActivityChance(difficulty))
                return false;

            // analysisAbility fixed at a high level - an AI doesn't depend on its own analyst
            // staff member to adjust its tactics.
            var report = ScoutingReportService.BuildReport(aiTeam, opponent, leagueStandings, leagueTeams, analysisAbility: 90);
            if (report.TacticSuggestion is not { } suggestion)
                return false;

            aiTeam.PlayingStyle = suggestion.Style;
            aiTeam.TacticalOrientation = suggestion.Orientation;
            return true;
        }
    }
}
