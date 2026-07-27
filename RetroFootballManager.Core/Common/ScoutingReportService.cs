using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Pre-match scouting report. Without an analyst on staff only the basic fields
    // (league position/form/avg rating) are set; the rest stay null.
    public record ScoutingReport(
        int OpponentPosition,
        string OpponentForm,
        double OpponentAverageRating,
        TeamStrengthProfile? OpponentProfile,
        TeamStrengthProfile? LeagueAverageProfile,
        string? WeaknessCategory,
        string? StrengthCategory,
        TacticSuggestion? TacticSuggestion,
        string? OpponentFormationName = null,
        PlayingStyle? OpponentPlayingStyle = null,
        TacticalOrientation? OpponentTacticalOrientation = null,
        List<string>? OpponentStartingXINames = null);

    public record TacticSuggestion(PlayingStyle Style, TacticalOrientation Orientation, string ExploitedCategory);

    // Builds the pre-match scouting report. Depth depends on the highest AnalysisAbility
    // among own analysts (no analyst -> only basic info).
    public static class ScoutingReportService
    {
        private const int WeaknessThreshold = 50;
        private const int TacticSuggestionThreshold = 75;
        private const int TopAnalystThreshold = 90;

        public static ScoutingReport BuildReport(
            Team ownTeam, Team opponent, List<StandingRow> leagueStandings, List<Team> leagueTeams, int? analysisAbility)
        {
            var row = leagueStandings.FirstOrDefault(r => r.TeamId == opponent.Id);
            int position = row?.Position ?? 0;
            string form = row?.Form ?? string.Empty;
            double averageRating = opponent.AverageRating;

            if (analysisAbility is null)
                return new ScoutingReport(position, form, averageRating, null, null, null, null, null);

            var opponentProfile = TeamStrengthCalculator.Calculate(opponent, isHome: false);
            var leagueAverageProfile = AverageProfile(leagueTeams);

            string? weakness = null, strength = null;
            if (analysisAbility >= WeaknessThreshold)
            {
                var diffs = CategoryDiffs(opponentProfile, leagueAverageProfile);
                weakness = diffs.MinBy(d => d.Diff).Category;
                strength = diffs.MaxBy(d => d.Diff).Category;
            }

            TacticSuggestion? suggestion = null;
            if (analysisAbility >= TacticSuggestionThreshold && weakness is not null)
                suggestion = BuildTacticSuggestion(ownTeam, opponentProfile, weakness);

            string? formationName = null;
            PlayingStyle? playingStyle = null;
            TacticalOrientation? tacticalOrientation = null;
            List<string>? startingXINames = null;
            if (analysisAbility >= TopAnalystThreshold)
            {
                formationName = opponent.FormationName;
                playingStyle = opponent.PlayingStyle;
                tacticalOrientation = opponent.TacticalOrientation;
                startingXINames = opponent.Players
                    .Where(p => p.Status == PlayerStatus.InStartingXI)
                    .Select(p => p.Name)
                    .ToList();
            }

            return new ScoutingReport(
                position, form, averageRating, opponentProfile, leagueAverageProfile, weakness, strength, suggestion,
                formationName, playingStyle, tacticalOrientation, startingXINames);
        }

        private static TeamStrengthProfile AverageProfile(List<Team> teams)
        {
            if (teams.Count == 0)
                return new TeamStrengthProfile(0, 0, 0, 0, 0, 0);

            var profiles = teams.Select(t => TeamStrengthCalculator.Calculate(t, isHome: false)).ToList();
            return new TeamStrengthProfile(
                Overall: profiles.Average(p => p.Overall),
                Attack: profiles.Average(p => p.Attack),
                Defense: profiles.Average(p => p.Defense),
                Midfield: profiles.Average(p => p.Midfield),
                Pressing: profiles.Average(p => p.Pressing),
                DisciplineRisk: profiles.Average(p => p.DisciplineRisk));
        }

        private static (string Category, double Diff)[] CategoryDiffs(
            TeamStrengthProfile profile, TeamStrengthProfile leagueAverage) =>
        [
            ("Angriff", profile.Attack - leagueAverage.Attack),
            ("Abwehr", profile.Defense - leagueAverage.Defense),
            ("Mittelfeld", profile.Midfield - leagueAverage.Midfield),
            ("Pressing", profile.Pressing - leagueAverage.Pressing),
        ];

        // Which own category most directly exploits an opponent weakness.
        private static double CategoryValue(TeamStrengthProfile profile, string category) => category switch
        {
            "Angriff" => profile.Attack,
            "Abwehr" => profile.Defense,
            "Mittelfeld" => profile.Midfield,
            "Pressing" => profile.Pressing,
            _ => profile.Overall,
        };

        private static readonly PlayingStyle[] Styles =
            [PlayingStyle.CounterAttack, PlayingStyle.TikiTaka, PlayingStyle.Pressing,
             PlayingStyle.WingPlay, PlayingStyle.CrossesToStriker];

        private static readonly TacticalOrientation[] Orientations =
            [TacticalOrientation.VeryDefensive, TacticalOrientation.Defensive, TacticalOrientation.Balanced,
             TacticalOrientation.Offensive, TacticalOrientation.VeryOffensive];

        private static TacticSuggestion BuildTacticSuggestion(
            Team ownTeam, TeamStrengthProfile opponentProfile, string opponentWeakness)
        {
            string exploitCategory = opponentWeakness switch
            {
                "Abwehr" => "Angriff",
                "Mittelfeld" => "Mittelfeld",
                "Pressing" => "Pressing",
                _ => "Abwehr",
            };
            double opponentWeaknessValue = CategoryValue(opponentProfile, opponentWeakness);

            var originalStyle = ownTeam.PlayingStyle;
            var originalOrientation = ownTeam.TacticalOrientation;

            try
            {
                PlayingStyle bestStyle = originalStyle;
                TacticalOrientation bestOrientation = originalOrientation;
                double bestDiff = double.MinValue;

                foreach (var style in Styles)
                {
                    foreach (var orientation in Orientations)
                    {
                        ownTeam.PlayingStyle = style;
                        ownTeam.TacticalOrientation = orientation;

                        var ownProfile = TeamStrengthCalculator.Calculate(ownTeam, isHome: false);
                        double diff = CategoryValue(ownProfile, exploitCategory) - opponentWeaknessValue;

                        if (diff > bestDiff)
                        {
                            bestDiff = diff;
                            bestStyle = style;
                            bestOrientation = orientation;
                        }
                    }
                }

                return new TacticSuggestion(bestStyle, bestOrientation, exploitCategory);
            }
            finally
            {
                ownTeam.PlayingStyle = originalStyle;
                ownTeam.TacticalOrientation = originalOrientation;
            }
        }
    }
}
