using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Sends the pre-match report (ScoutingReportService) as a mailbox message before every
    // match (league/cup/friendly) - only if an analyst is employed. Pure text formatting,
    // no MAUI dependency (unlike the MAUI-side PlayingStyleOption/OrientationOption labels -
    // this has its own small switch labels).
    public static class PreMatchAnalysisService
    {
        public static async Task SendIfAnalystEmployedAsync(
            MessageService messages, Team humanTeam, Team opponent, List<StandingRow> standings,
            List<Team> comparisonPool, DateTime currentDate, string matchLabel)
        {
            int? ability = humanTeam.Employees
                .Where(e => e.EmployeeType == EmployeeType.Analyst)
                .Select(e => (int?)e.AnalysisAbility)
                .Max();
            if (ability is null)
                return;

            var report = ScoutingReportService.BuildReport(humanTeam, opponent, standings, comparisonPool, ability);
            string body = FormatBody(report, opponent, ability.Value);
            await messages.SendAsync(MessageType.OpponentAnalysis, $"Analyse: {matchLabel}", body, currentDate, opponent.Id);
        }

        private static string FormatBody(ScoutingReport report, Team opponent, int ability)
        {
            var lines = new List<string>
            {
                $"Gegner: {opponent.Name} (Tabellenplatz {report.OpponentPosition}, Form: {report.OpponentForm})",
                $"Ø-Rating: {report.OpponentAverageRating:0.0}",
            };

            if (report.OpponentProfile is { } profile)
                lines.Add($"Angriff {profile.Attack:0} · Abwehr {profile.Defense:0} · " +
                          $"Mittelfeld {profile.Midfield:0} · Pressing {profile.Pressing:0}");

            if (report.WeaknessCategory is not null)
                lines.Add($"Schwäche: {report.WeaknessCategory} · Stärke: {report.StrengthCategory}");

            if (report.TacticSuggestion is not null)
                lines.Add($"Taktik-Tipp: {StyleLabel(report.TacticSuggestion.Style)} / " +
                          $"{OrientationLabel(report.TacticSuggestion.Orientation)} (zielt auf {report.TacticSuggestion.ExploitedCategory})");

            if (ability >= 90 && report.OpponentFormationName is not null)
            {
                lines.Add($"Formation: {report.OpponentFormationName} · Stil: {StyleLabel(report.OpponentPlayingStyle!.Value)} · " +
                          $"Ausrichtung: {OrientationLabel(report.OpponentTacticalOrientation!.Value)}");
                if (report.OpponentStartingXINames is { Count: > 0 })
                    lines.Add($"Aufstellung: {string.Join(", ", report.OpponentStartingXINames)}");
            }

            return string.Join("\n", lines);
        }

        private static string StyleLabel(PlayingStyle style) => style switch
        {
            PlayingStyle.CounterAttack => "Konter",
            PlayingStyle.TikiTaka => "Tiki-Taka",
            PlayingStyle.Pressing => "Pressing",
            PlayingStyle.WingPlay => "Flügelspiel",
            PlayingStyle.CrossesToStriker => "Flanken auf Stürmer",
            _ => style.ToString(),
        };

        private static string OrientationLabel(TacticalOrientation orientation) => orientation switch
        {
            TacticalOrientation.VeryDefensive => "sehr defensiv",
            TacticalOrientation.Defensive => "defensiv",
            TacticalOrientation.Balanced => "ausgeglichen",
            TacticalOrientation.Offensive => "offensiv",
            TacticalOrientation.VeryOffensive => "sehr offensiv",
            _ => orientation.ToString(),
        };
    }
}
