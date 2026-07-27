using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public enum StatCategory
    {
        TopScorers,
        TopAssists,
        ScorerPoints,
        YellowCards,
        RedCards,
        FewestConceded,
    }

    public record PlayerStatRow(int PlayerId, string PlayerName, string TeamName, string PositionShort, double Value);

    // Leaderboards for a single league/season, built on top of the season PlayerStats rows.
    public class PlayerStatsService
    {
        private readonly PlayerRepository _players;
        private readonly TeamRepository _teams;

        public PlayerStatsService(PlayerRepository players, TeamRepository teams)
        {
            _players = players;
            _teams = teams;
        }

        // Every player of the given league tier with their season PlayerStats row (players
        // without any recorded appearances this season are skipped - nothing to rank yet).
        public async Task<List<(Player Player, Team Team, PlayerStats Stats)>> GetLeagueSeasonStatsAsync(int season, int leagueTier)
        {
            var teams = (await _teams.GetAllTeamsAsync()).Where(t => t.LeagueTier == leagueTier).ToList();
            var rows = new List<(Player, Team, PlayerStats)>();

            foreach (var team in teams)
            {
                foreach (var player in team.Players)
                {
                    var stats = (await _players.GetPlayerStatsAsync(player.Id, season)).FirstOrDefault();
                    if (stats is not null)
                        rows.Add((player, team, stats));
                }
            }

            return rows;
        }

        // Ranks GetLeagueSeasonStatsAsync's rows for one category. matchdaysPlayed drives the
        // FewestConceded minimum-appearances cutoff (>= half the matchdays played so far) -
        // without it a keeper with one substitute cameo and zero goals conceded would top the
        // list ahead of every regular starter.
        public async Task<List<PlayerStatRow>> GetTopAsync(
            StatCategory category, int season, int leagueTier, int matchdaysPlayed, int take = 10)
        {
            var rows = await GetLeagueSeasonStatsAsync(season, leagueTier);
            if (category == StatCategory.FewestConceded)
                rows = rows.Where(r => r.Player.Position == Position.Goalkeeper
                                        && r.Stats.Appearances >= Math.Max(1, matchdaysPlayed / 2))
                           .ToList();

            return RankAndSelect(rows, category, take);
        }

        // Every player with a recorded PlayerStats row for this competition/season - competition
        // stats have no matchday-count concept (small cup sample sizes), so FewestConceded just
        // requires at least one appearance instead of a matchdays-played-based cutoff.
        public async Task<List<PlayerStatRow>> GetCompetitionTopAsync(
            StatCategory category, int season, CompetitionType competition, int take = 10)
        {
            var statsRows = await _players.GetStatsByCompetitionAsync(season, competition);
            var rows = new List<(Player Player, Team Team, PlayerStats Stats)>();
            foreach (var stats in statsRows)
            {
                var player = await _players.GetPlayerAsync(stats.PlayerId);
                if (player is null) continue;
                var team = await _teams.GetTeamAsync(player.TeamId);
                if (team is null) continue;
                rows.Add((player, team, stats));
            }

            if (category == StatCategory.FewestConceded)
                rows = rows.Where(r => r.Player.Position == Position.Goalkeeper && r.Stats.Appearances > 0).ToList();

            return RankAndSelect(rows, category, take);
        }

        private static List<PlayerStatRow> RankAndSelect(
            List<(Player Player, Team Team, PlayerStats Stats)> rows, StatCategory category, int take)
        {
            var ranked = category == StatCategory.FewestConceded
                ? rows.OrderBy(r => r.Stats.GoalsConceded)
                    .ThenByDescending(r => r.Stats.Appearances).ThenBy(r => r.Player.Name)
                : rows.OrderByDescending(r => ValueFor(category, r.Stats))
                    .ThenByDescending(r => r.Stats.Appearances).ThenBy(r => r.Player.Name);

            return ranked
                .Take(take)
                .Select(r => new PlayerStatRow(
                    r.Player.Id,
                    r.Player.Name,
                    r.Team.ShortName,
                    r.Player.ShortPositionName,
                    ValueFor(category, r.Stats)))
                .ToList();
        }

        private static double ValueFor(StatCategory category, PlayerStats stats) => category switch
        {
            StatCategory.TopScorers => stats.Goals,
            StatCategory.TopAssists => stats.Assists,
            StatCategory.ScorerPoints => stats.Goals + stats.Assists,
            StatCategory.YellowCards => stats.YellowCards,
            StatCategory.RedCards => stats.RedCards,
            StatCategory.FewestConceded => stats.GoalsConceded,
            _ => 0,
        };
    }
}
