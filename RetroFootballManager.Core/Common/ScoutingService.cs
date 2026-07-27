using RetroFootballManager.Core.Models;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public record ScoutRecommendation(int PlayerId, string PlayerName, string TeamName, Position Position, string Reason);

    // Individual player scouting (selection + 2-week wait) + proactive scout recommendations
    // for understaffed/weak positions. Separate from ScoutingReportService (that's the
    // analyst's opponent analysis, a different staff type).
    public static class ScoutingService
    {
        private const int ScoutingDurationDays = 14;

        // Positions with fewer players than this count as understaffed (goalkeeper needs
        // fewer backups than outfield positions).
        private const int MinPlayersPerPosition = 2;
        private const int MinGoalkeepers = 1;

        public static bool HasScout(Team team) =>
            team.Employees.Any(e => e.EmployeeType == EmployeeType.Scout);

        public static int? BestScoutingAbility(Team team) =>
            team.Employees.Where(e => e.EmployeeType == EmployeeType.Scout)
                .Select(e => (int?)e.ScoutingAbility).Max();

        // Pure pre-check (no DB access) - the running-assignment check runs async via
        // SaveGameService.TryStartScoutingAsync (needs the repository query).
        public static bool TryStartScouting(Team team, Player player, out string? error)
        {
            error = null;
            if (!HasScout(team))
            {
                error = "Sie haben aktuell keinen Scout angestellt.";
                return false;
            }
            if (player.IsScouted)
            {
                error = "Spieler bereits gescoutet.";
                return false;
            }
            return true;
        }

        public static ScoutingAssignment CreateAssignment(int teamId, int playerId, DateTime currentDate) => new()
        {
            TeamId = teamId,
            PlayerId = playerId,
            StartDate = currentDate,
            CompletionDate = currentDate.AddDays(ScoutingDurationDays),
        };

        // Picks out the 1-2 weakest-staffed own positions (understaffed OR average rating
        // well below the squad average) and looks for matching candidates on other teams in
        // the SAME league tier. Deterministic per (team, season, month) - the same view gives
        // stable results within a month but changes from month to month (several suggestions
        // spread over the season, without its own persistence).
        public static List<ScoutRecommendation> GetRecommendations(
            Team team, IReadOnlyList<Team> allTeams, int season, int month, int take = 5)
        {
            var weakPositions = FindWeakPositions(team);
            if (weakPositions.Count == 0)
                return [];

            int? ability = BestScoutingAbility(team);
            if (ability is null)
                return [];

            var rng = new Random(HashCode.Combine(team.Id, season, month));
            var ownPlayerIds = team.Players.Select(p => p.Id).ToHashSet();

            var candidates = allTeams
                .Where(t => t.Id != team.Id && t.LeagueTier == team.LeagueTier)
                .SelectMany(t => t.Players.Where(p =>
                        weakPositions.Contains(p.Position) && !ownPlayerIds.Contains(p.Id) && !p.IsScouted)
                    .Select(p => (Player: p, Team: t)))
                .ToList();

            var scored = candidates.Select(c =>
            {
                double noise = (rng.NextDouble() * 2 - 1) * (100 - ability.Value) / 100.0 * 20;
                double talentBonus = (ability.Value / 100.0) * Math.Max(0, c.Player.Talent - c.Player.Rating);
                double score = c.Player.Rating + noise + talentBonus;
                return (c.Player, c.Team, Score: score);
            });

            return scored
                .OrderByDescending(s => s.Score)
                .Take(take)
                .Select(s => new ScoutRecommendation(
                    s.Player.Id, s.Player.Name, s.Team.ShortName, s.Player.Position,
                    $"Passt zur unterbesetzten Position {PositionLabel(s.Player.Position)}"))
                .ToList();
        }

        private static HashSet<Position> FindWeakPositions(Team team)
        {
            var groups = team.Players.GroupBy(p => p.Position)
                .ToDictionary(g => g.Key, g => (Count: g.Count(), AverageRating: g.Average(p => p.Rating)));
            double squadAverage = team.Players.Count > 0 ? team.Players.Average(p => p.Rating) : 0;

            var weak = new HashSet<Position>();
            foreach (Position position in Enum.GetValues<Position>())
            {
                int minCount = position == Position.Goalkeeper ? MinGoalkeepers : MinPlayersPerPosition;
                if (!groups.TryGetValue(position, out var stats))
                {
                    weak.Add(position);
                    continue;
                }
                if (stats.Count < minCount || stats.AverageRating < squadAverage - 5)
                    weak.Add(position);
            }
            return weak;
        }

        private static string PositionLabel(Position position) => PositionDisplay.Short(position);
    }
}
