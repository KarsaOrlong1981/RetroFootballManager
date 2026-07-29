using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Derives whether a team ever entered a cup, is still in it, got knocked out, or won it -
    // purely from its CupTie rows for one season/competition. No status is persisted; it's
    // always recomputed from the tie history (see CupTieRepository.GetParticipationStatusAsync).
    public static class CupParticipationService
    {
        public static CupParticipationStatus GetStatus(int teamId, IReadOnlyList<CupTie> seasonCompetitionTies)
        {
            var relevant = seasonCompetitionTies.Where(t => t.HomeTeamId == teamId || t.AwayTeamId == teamId).ToList();
            if (relevant.Count == 0)
                return CupParticipationStatus.NotEntered;

            int maxRound = relevant.Max(t => t.Round);

            // Group stage (Round 0): elimination isn't decided by a single tie but by the final
            // group table (top 2 advance) - need every tie of that group, not just the team's own.
            if (maxRound == 0)
            {
                var group = relevant.First().Group;
                var groupTies = seasonCompetitionTies.Where(t => t.Group == group).ToList();
                if (groupTies.Any(t => !t.Played))
                    return CupParticipationStatus.StillIn;

                var table = GroupDrawService.CalculateGroupTable(groupTies, new Dictionary<int, string>());
                int position = table.First(r => r.TeamId == teamId).Position;
                return position <= 2 ? CupParticipationStatus.StillIn : CupParticipationStatus.Eliminated;
            }

            var lastRoundTies = relevant.Where(t => t.Round == maxRound).ToList();
            if (lastRoundTies.Any(t => !t.Played))
                return CupParticipationStatus.StillIn;

            int winnerId = CupTieHelper.DetermineAggregateWinner(lastRoundTies);
            if (winnerId != teamId)
                return CupParticipationStatus.Eliminated;

            return maxRound == CupDrawService.RoundFinal
                ? CupParticipationStatus.Won
                : CupParticipationStatus.StillIn;
        }
    }
}
