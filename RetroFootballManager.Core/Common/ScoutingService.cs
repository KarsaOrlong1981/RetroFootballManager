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
        private const int MinPlayersPerPosition = 3;
        private const int MinGoalkeepers = 1;

        // Each scout can carry at most this many concurrent scouting assignments - assigning
        // more (manually or via a ScoutingFocus) is rejected until some complete.
        public const int MaxConcurrentAssignmentsPerScout = 6;

        // Noise spread for the league-average position comparison (ScoutingFocus default),
        // same shape as GetRecommendations' existing noise term.
        private const double LeagueComparisonNoiseSpread = 20;

        // How many players must be at/above the (possibly noisy) league-average rating for a
        // position to be considered adequately staffed.
        private const int MinPlayersAtOrAboveLeagueAverage = 2;

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

        public static ScoutingAssignment CreateAssignment(int teamId, int playerId, DateTime currentDate, int scoutEmployeeId) => new()
        {
            TeamId = teamId,
            PlayerId = playerId,
            ScoutEmployeeId = scoutEmployeeId,
            StartDate = currentDate,
            CompletionDate = currentDate.AddDays(ScoutingDurationDays),
        };

        // Picks the scout with the fewest active assignments who still has free capacity
        // (< MaxConcurrentAssignmentsPerScout) - null if no scout has room (or none employed).
        public static Employee? FindScoutWithCapacity(Team team, IReadOnlyList<ScoutingAssignment> activeAssignments)
        {
            var scouts = team.Employees.Where(e => e.EmployeeType == EmployeeType.Scout).ToList();
            if (scouts.Count == 0)
                return null;

            var loadByScout = activeAssignments
                .Where(a => a.ScoutEmployeeId != 0)
                .GroupBy(a => a.ScoutEmployeeId)
                .ToDictionary(g => g.Key, g => g.Count());

            return scouts
                .Select(s => (Scout: s, Load: loadByScout.GetValueOrDefault(s.Id)))
                .Where(x => x.Load < MaxConcurrentAssignmentsPerScout)
                .OrderBy(x => x.Load)
                .Select(x => x.Scout)
                .FirstOrDefault();
        }

        // Precheck for assigning a new ScoutingFocus to a specific scout - rejects while that
        // scout is already at capacity.
        public static bool TryAssignFocus(Employee scout, IReadOnlyList<ScoutingAssignment> activeAssignments, out string? error)
        {
            int load = activeAssignments.Count(a => a.ScoutEmployeeId == scout.Id);
            if (load >= MaxConcurrentAssignmentsPerScout)
            {
                error = $"{scout.Name} ist derzeit ausgebucht ({MaxConcurrentAssignmentsPerScout}/{MaxConcurrentAssignmentsPerScout}) - er muss erst seine aktuellen Aufgaben abschließen.";
                return false;
            }
            error = null;
            return true;
        }

        // Whether a position is understaffed relative to the rest of the league (not just the
        // own squad average, unlike FindWeakPositions) - at least MinPlayersAtOrAboveLeagueAverage
        // own players must be at/above the league's average rating for this position. The
        // comparison is noisy, scaled inversely to the scout's ability (same pattern as
        // GetRecommendations' scoring noise), so a weak scout's assessment can be wrong either way.
        public static bool EvaluatePositionAgainstLeague(
            Team team, Position position, IReadOnlyList<Team> leagueTeams, int scoutAbility, Random rng)
        {
            var ownPlayers = team.Players.Where(p => p.Position == position).ToList();
            int minCount = position == Position.Goalkeeper ? MinGoalkeepers : MinPlayersPerPosition;
            if (ownPlayers.Count < minCount)
                return true;

            var leaguePlayers = leagueTeams
                .Where(t => t.LeagueTier == team.LeagueTier)
                .SelectMany(t => t.Players.Where(p => p.Position == position))
                .ToList();
            if (leaguePlayers.Count == 0)
                return false;

            double leagueAverage = leaguePlayers.Average(p => p.Rating);
            double noise = (rng.NextDouble() * 2 - 1) * (100 - scoutAbility) / 100.0 * LeagueComparisonNoiseSpread;
            double perceivedLeagueAverage = leagueAverage + noise;

            int atOrAboveAverage = ownPlayers.Count(p => p.Rating >= perceivedLeagueAverage);
            return atOrAboveAverage < MinPlayersAtOrAboveLeagueAverage;
        }

        private static HashSet<Position> FindWeakPositionsAgainstLeague(
            Team team, IReadOnlyList<Team> leagueTeams, int scoutAbility, Random rng)
        {
            var weak = new HashSet<Position>();
            foreach (Position position in Enum.GetValues<Position>())
            {
                if (EvaluatePositionAgainstLeague(team, position, leagueTeams, scoutAbility, rng))
                    weak.Add(position);
            }
            return weak;
        }

        // Candidates matching a ScoutingFocus's filters (or, if none are set, the team-weakness
        // fallback via FindWeakPositionsAgainstLeague) - same pool restriction as
        // GetRecommendations (same league tier, not already own/scouted).
        public static List<Player> FindCandidatesForFocus(
            Team team, ScoutingFocus focus, IReadOnlyList<Team> allTeams, Random rng, int take = 10)
        {
            var ownPlayerIds = team.Players.Select(p => p.Id).ToHashSet();
            IEnumerable<Player> pool = allTeams
                .Where(t => t.Id != team.Id && t.LeagueTier == team.LeagueTier)
                .SelectMany(t => t.Players)
                .Where(p => !ownPlayerIds.Contains(p.Id) && !p.IsScouted);

            if (focus.HasAnyFilter)
            {
                if (focus.Position is { } position) pool = pool.Where(p => p.Position == position);
                if (focus.MinAge is { } minAge) pool = pool.Where(p => p.Age >= minAge);
                if (focus.MaxAge is { } maxAge) pool = pool.Where(p => p.Age <= maxAge);
                if (focus.MinTalent is { } minTalent) pool = pool.Where(p => p.Talent >= minTalent);
                if (focus.MinRating is { } minRating) pool = pool.Where(p => p.Rating >= minRating);
                if (focus.CharacterType is { } characterType) pool = pool.Where(p => p.InMatchCharacter == characterType);
                if (focus.PersonalityType is { } personalityType) pool = pool.Where(p => p.Personality == personalityType);
                if (focus.Nationality is { } nationality) pool = pool.Where(p => p.Nationality == nationality);
                foreach (var attributeFilter in focus.AttributeFilters)
                {
                    var filter = attributeFilter;
                    pool = pool.Where(p => PlayerAttributeAccessor.GetValue(p, filter.Attribute) >= filter.MinValue);
                }

                return pool.OrderByDescending(p => p.Rating).Take(take).ToList();
            }

            int ability = team.Employees.FirstOrDefault(e => e.Id == focus.ScoutEmployeeId)?.ScoutingAbility ?? 50;
            var weakPositions = FindWeakPositionsAgainstLeague(team, allTeams, ability, rng);
            return pool.Where(p => weakPositions.Contains(p.Position))
                .OrderByDescending(p => p.Rating)
                .Take(take)
                .ToList();
        }

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
