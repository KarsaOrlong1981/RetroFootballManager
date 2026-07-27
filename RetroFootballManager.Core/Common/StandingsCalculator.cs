using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // A league table row. Form = last up to 5 results as "WDLWW" (oldest
    // first, most recent last).
    public record StandingRow(
        int Position,
        int TeamId,
        string TeamName,
        int Played,
        int Wins,
        int Draws,
        int Losses,
        int GoalsFor,
        int GoalsAgainst,
        int GoalDifference,
        int Points,
        string Form);

    // Calculates league standings from played fixtures. Independent of TeamStats,
    // so historical/other leagues can always be derived from the schedule.
    public static class StandingsCalculator
    {
        private const int FormLength = 5;

        public static List<StandingRow> Calculate(
            IReadOnlyList<Fixture> leagueFixtures,
            IReadOnlyDictionary<int, string> teamNames)
        {
            var acc = new Dictionary<int, Agg>();

            Agg Get(int teamId)
            {
                if (!acc.TryGetValue(teamId, out var a))
                {
                    a = new Agg();
                    acc[teamId] = a;
                }
                return a;
            }

            // Include all involved teams (even without played matches -> 0 points).
            foreach (var f in leagueFixtures)
            {
                Get(f.HomeTeamId);
                Get(f.AwayTeamId);
            }

            // Process chronologically (matchday) so the form (last 5) is correct.
            foreach (var f in leagueFixtures.Where(f => f.Played).OrderBy(f => f.Matchday))
            {
                var home = Get(f.HomeTeamId);
                var away = Get(f.AwayTeamId);

                home.Played++; away.Played++;
                home.GoalsFor += f.HomeGoals; home.GoalsAgainst += f.AwayGoals;
                away.GoalsFor += f.AwayGoals; away.GoalsAgainst += f.HomeGoals;

                char homeResult, awayResult;
                if (f.HomeGoals > f.AwayGoals) { home.Wins++; away.Losses++; homeResult = 'W'; awayResult = 'L'; }
                else if (f.HomeGoals < f.AwayGoals) { away.Wins++; home.Losses++; homeResult = 'L'; awayResult = 'W'; }
                else { home.Draws++; away.Draws++; homeResult = 'D'; awayResult = 'D'; }

                home.AppendForm(homeResult);
                away.AppendForm(awayResult);
            }

            var rows = acc
                .Select(kv => new
                {
                    TeamId = kv.Key,
                    A = kv.Value,
                })
                .OrderByDescending(x => x.A.Points)
                .ThenByDescending(x => x.A.GoalsFor - x.A.GoalsAgainst)
                .ThenByDescending(x => x.A.GoalsFor)
                .ThenBy(x => teamNames.TryGetValue(x.TeamId, out var n) ? n : string.Empty)
                .ToList();

            return rows.Select((x, index) => new StandingRow(
                index + 1,
                x.TeamId,
                teamNames.TryGetValue(x.TeamId, out var name) ? name : $"Team {x.TeamId}",
                x.A.Played, x.A.Wins, x.A.Draws, x.A.Losses,
                x.A.GoalsFor, x.A.GoalsAgainst, x.A.GoalsFor - x.A.GoalsAgainst,
                x.A.Points, x.A.Form)).ToList();
        }

        private sealed class Agg
        {
            public int Played, Wins, Draws, Losses, GoalsFor, GoalsAgainst;
            public int Points => (Wins * 3) + Draws;
            public string Form { get; private set; } = string.Empty;

            public void AppendForm(char result)
            {
                Form += result;
                if (Form.Length > FormLength)
                    Form = Form[^FormLength..];
            }
        }
    }
}
