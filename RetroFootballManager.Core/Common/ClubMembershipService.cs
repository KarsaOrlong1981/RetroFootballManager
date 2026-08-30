using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public static class ClubMembershipService
    {
        private static readonly (int Min, int Max)[] TierMemberRange =
        [
            (120_000, 150_000), (40_000, 60_000), (10_000, 20_000), (3_000, 10_000),
        ];

        private static readonly (int Min, int Max)[] TierFeeRange =
        [
            (80, 150), (60, 130), (70, 120), (40, 90),
        ];

        private static readonly double[] TierTargetRating = [77, 67, 57, 47];

        private const double RatingNormalizationBand = 30.0;

        public static (int Members, int Fee) ForTierAndRating(int tier, double rating)
        {
            double target = TierTargetRating[tier - 1];
            double t = Math.Clamp((rating - (target - RatingNormalizationBand / 2)) / RatingNormalizationBand, 0, 1);

            var (memberMin, memberMax) = TierMemberRange[tier - 1];
            var (feeMin, feeMax) = TierFeeRange[tier - 1];
            int members = memberMin + (int)Math.Round((memberMax - memberMin) * t);
            int fee = feeMin + (int)Math.Round((feeMax - feeMin) * t);
            return (members, fee);
        }

        // Quarterly checkpoints across a 34-matchday season.
        private static readonly int[] QuarterCheckpoints = [9, 17, 26, 34];

        private static readonly int[] TierQuarterMemberDelta = [500, 200, 80, 30];

        public static bool IsQuarterCheckpoint(int matchday) => QuarterCheckpoints.Contains(matchday);

        public static int ApplyQuarterlyPerformance(Team team, StandingRow row, int matchday)
        {
            var finances = team.Finances;
            if (finances is null || !IsQuarterCheckpoint(matchday))
                return 0;

            int quarterPlayed = row.Played - finances.MembershipCheckMatchday;
            int quarterPoints = row.Points - finances.MembershipCheckPoints;
            finances.MembershipCheckMatchday = row.Played;
            finances.MembershipCheckPoints = row.Points;

            if (quarterPlayed <= 0)
                return 0;

            double ppg = quarterPoints / (double)quarterPlayed;
            int baseDelta = TierQuarterMemberDelta[team.LeagueTier - 1];

            int delta = ppg switch
            {
                >= 2.0 => baseDelta,
                >= 1.3 => baseDelta / 2,
                <= 0.7 => -baseDelta,
                _ => 0,
            };

            if (delta != 0)
                finances.ClubMembers += delta;

            return delta;
        }

        private static readonly int[] TierChampionshipBonus = [4000, 1800, 600, 250];
        private const int ChampionsLeagueQualificationBonus = 2500;
        private const int EuropaCupQualificationBonus = 1200;

        private static readonly Dictionary<int, int> PromotionBonusByOldTier = new() { [2] = 2500, [3] = 1000, [4] = 400 };
        private static readonly Dictionary<int, int> RelegationMalusByOldTier = new() { [1] = -2500, [2] = -1200, [3] = -500 };

        public static void ApplySeasonEndAdjustments(IReadOnlyList<Team> teams, SeasonEndResult result)
        {
            var teamsById = teams.ToDictionary(t => t.Id);
            var oldTierByTeamId = new Dictionary<int, int>();

            foreach (var league in result.Leagues)
            {
                foreach (var teamId in league.PromotedTeamIds)
                    oldTierByTeamId[teamId] = league.Tier;
                foreach (var teamId in league.RelegatedTeamIds)
                    oldTierByTeamId[teamId] = league.Tier;

                foreach (var row in league.Table)
                {
                    if (!teamsById.TryGetValue(row.TeamId, out var team) || team.Finances is null)
                        continue;

                    int delta = 0;
                    if (row.Position == 1)
                        delta += TierChampionshipBonus[league.Tier - 1];
                    if (league.Tier == 1)
                        delta += row.Position <= 4 ? ChampionsLeagueQualificationBonus
                            : row.Position <= 7 ? EuropaCupQualificationBonus : 0;
                    if (league.PromotedTeamIds.Contains(row.TeamId))
                        delta += PromotionBonusByOldTier.GetValueOrDefault(league.Tier);
                    if (league.RelegatedTeamIds.Contains(row.TeamId))
                        delta += RelegationMalusByOldTier.GetValueOrDefault(league.Tier);

                    if (delta != 0)
                        team.Finances.ClubMembers = Math.Max(0, team.Finances.ClubMembers + delta);
                }
            }

            foreach (var (teamId, oldTier) in oldTierByTeamId)
            {
                if (!teamsById.TryGetValue(teamId, out var team) || team.Finances is null || team.LeagueTier == oldTier)
                    continue;

                var (memberMin, memberMax) = TierMemberRange[team.LeagueTier - 1];
                team.Finances.MembershipFeePerMember = ForTierAndRating(team.LeagueTier, team.AverageRating).Fee;
                team.Finances.ClubMembers = Math.Clamp(team.Finances.ClubMembers, memberMin, memberMax);
            }
        }

        // Cumulative fee for reaching a German Cup round, paid once per distinct round reached
        // same derivation style as PrizeMoneyService.AwardCupPrizes.
        private static readonly Dictionary<int, int> RoundReachedMemberBonus = new()
        {
            [CupDrawService.RoundLastSixtyFour] = 30,
            [CupDrawService.RoundLastThirtyTwo] = 60,
            [CupDrawService.RoundLastSixteen] = 120,
            [CupDrawService.RoundQuarterFinal] = 250,
            [CupDrawService.RoundSemiFinal] = 500,
            [CupDrawService.RoundFinal] = 1000,
        };

        private const int CupWinnerBonus = 2000;
        private const int CupFinalistBonus = 800;
        private const int CupSemiFinalOutBonus = 300;

        public static void ApplyCupPrizes(IReadOnlyList<Team> teams, IReadOnlyList<CupTie> germanCupTies)
        {
            if (germanCupTies.Count == 0)
                return;

            var teamsById = teams.ToDictionary(t => t.Id);
            var teamIds = germanCupTies
                .SelectMany(t => t.IsBye ? [t.HomeTeamId] : new[] { t.HomeTeamId, t.AwayTeamId })
                .Distinct();

            foreach (var teamId in teamIds)
            {
                if (!teamsById.TryGetValue(teamId, out var team) || team.Finances is null)
                    continue;

                var participated = germanCupTies.Where(t => t.HomeTeamId == teamId || (!t.IsBye && t.AwayTeamId == teamId)).ToList();
                if (participated.Count == 0)
                    continue;

                int maxRound = participated.Max(t => t.Round);
                var finalRoundTies = participated.Where(t => t.Round == maxRound).ToList();
                if (finalRoundTies.Any(t => !t.Played))
                    continue;

                int bonus = participated.Select(t => t.Round).Distinct().Sum(round => RoundReachedMemberBonus.GetValueOrDefault(round));

                if (maxRound == CupDrawService.RoundFinal || maxRound == CupDrawService.RoundSemiFinal)
                {
                    bool won = finalRoundTies.Any(t => t.IsBye) || CupTieHelper.DetermineAggregateWinner(finalRoundTies) == teamId;
                    if (maxRound == CupDrawService.RoundFinal)
                        bonus += won ? CupWinnerBonus : CupFinalistBonus;
                    else if (!won)
                        bonus += CupSemiFinalOutBonus;
                }

                if (bonus > 0)
                    team.Finances.ClubMembers += bonus;
            }
        }
    }
}
