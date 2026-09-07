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

        // Membership recruitment campaign, launchable from the Merchandise department - a
        // campaign means actually offering members something for their money, so the cost
        // scales with BOTH league tier AND the club's current membership fee (a club charging
        // more per member has to spend more to justify it), floored at 15,000 € per explicit
        // user request. Runs for CampaignDurationDays and members trickle in linearly day by
        // day via ApplyCampaignDrip - no second campaign can start while one is still running
        // (IsCampaignActive).
        private const int CampaignDurationDays = 75; // ~2.5 months, per user's "2 oder 3 Monate"
        private const int MinCampaignCost = 15_000;

        // Baseline cost AT the tier's reference (mid-range) membership fee - see
        // GetCampaignCost. A club charging above that reference pays proportionally more,
        // below it proportionally less (still floored at MinCampaignCost).
        private static readonly int[] TierCampaignCost = [60_000, 35_000, 22_000, 15_000];

        // Total members gained over the full campaign at a NEUTRAL (1.0) performance factor -
        // scaled up/down by the club's actual table position/form at launch time (see
        // MerchandiseSalesCalculator.PerformanceFactor, reused here so "how well is the season
        // going" drives campaign success the same way it drives merchandise demand).
        private static readonly int[] TierCampaignBaseGain = [4000, 1800, 700, 250];

        // Hard ceiling on ClubMembers - campaigns are the only way membership can grow without
        // any other bound (unlike ApplySeasonEndAdjustments' promotion/relegation clamp, which
        // only fires on a tier change), so left uncapped this could run away over many seasons
        // of repeated campaigns. ~1.4x the natural TierMemberRange ceiling still allows a
        // well-run club real growth headroom without becoming absurd.
        private static readonly int[] TierMemberHardCap = [200_000, 85_000, 30_000, 15_000];

        public static int GetCampaignCost(int leagueTier, int membershipFeePerMember)
        {
            var (feeMin, feeMax) = TierFeeRange[leagueTier - 1];
            double referenceFee = (feeMin + feeMax) / 2.0;
            double feeFactor = membershipFeePerMember > 0 ? membershipFeePerMember / referenceFee : 1.0;
            int cost = (int)Math.Round(TierCampaignCost[leagueTier - 1] * feeFactor);
            return Math.Max(MinCampaignCost, cost);
        }

        public static bool IsCampaignActive(Finances finances, DateTime currentDate) =>
            finances.MembershipCampaignEndDate is { } end && currentDate < end;

        // A Director of Football is required to run a campaign at all (not just for the
        // Merchandise page's purchase recommendations) - makes hiring one a genuinely
        // worthwhile decision, per explicit user request. Same for AI teams (see
        // TryRunAiCampaignTick) - full parity with the human, no special-casing.
        public static bool HasDirectorOfFootball(Team team) =>
            team.Employees.Any(e => e.EmployeeType == EmployeeType.DirectorOfFootball);

        // performanceFactor: pass MerchandiseSalesCalculator.PerformanceFactor(standingRow,
        // leagueSize) - evaluated ONCE at launch (a snapshot of "how attractive is the club
        // right now"), not re-evaluated during the drip.
        public static bool TryLaunchCampaign(Team team, DateTime currentDate, double performanceFactor, Random random)
        {
            var finances = team.Finances;
            if (finances is null || !HasDirectorOfFootball(team) || IsCampaignActive(finances, currentDate))
                return false;

            int cost = GetCampaignCost(team.LeagueTier, finances.MembershipFeePerMember);
            if (finances.CurrentBalance < cost)
                return false;

            finances.CurrentBalance -= cost;

            int baseGain = TierCampaignBaseGain[team.LeagueTier - 1];
            double jitter = 0.85 + random.NextDouble() * 0.3; // +/-15%, texture only
            int totalGain = Math.Max(0, (int)Math.Round(baseGain * performanceFactor * jitter));

            finances.MembershipCampaignStartDate = currentDate;
            finances.MembershipCampaignEndDate = currentDate.AddDays(CampaignDurationDays);
            finances.MembershipCampaignTotalGain = totalGain;
            finances.MembershipCampaignAppliedGain = 0;
            return true;
        }

        // AI counterpart to TryLaunchCampaign - same rules (needs a DoF, cost, no overlap),
        // plus two AI-only guards: cautionFactor (pass FinanceAiService.ComputeCautionFactor -
        // same "don't spend while in trouble" gate AiManagerService already applies to stadium
        // upgrades/staff hires) and a difficulty-scaled weekly chance, so AI teams don't all
        // launch a campaign the instant they can afford one. Both real DoF-gating and the
        // caution/chance gating are what keep this from "ausarten" across many AI teams over a
        // season (verified by MerchandiseSeasonValidationTests).
        private static readonly double[] DifficultyCampaignChance = [0.03, 0.06, 0.10]; // Easy/Normal/Hard, per week

        public static bool TryRunAiCampaignTick(
            Team team, DateTime currentDate, double performanceFactor, Difficulty difficulty, double cautionFactor, Random random)
        {
            if (team.Finances is null || cautionFactor <= 0.2)
                return false;

            if (random.NextDouble() > DifficultyCampaignChance[(int)difficulty])
                return false;

            return TryLaunchCampaign(team, currentDate, performanceFactor, random);
        }

        // Daily drip - call from both CalendarAdvanceService's daily human tick AND
        // MatchDayService.ApplyFinanceAsync (a matchday advances the date without ever going
        // through CalendarAdvanceService, same dual-hook reasoning as FinanceService.
        // ApplyMonthlySettlementAsync). Idempotent per date: computes how much SHOULD have
        // been applied by elapsed-time-fraction and only adds the delta, so calling it twice
        // on the same day never double-applies. Returns the members added this call (0 if
        // no active campaign or nothing new due yet).
        public static int ApplyCampaignDrip(Team team, DateTime currentDate)
        {
            var finances = team.Finances;
            if (finances?.MembershipCampaignStartDate is not { } start || finances.MembershipCampaignEndDate is not { } end)
                return 0;

            double totalDays = Math.Max(1, (end - start).TotalDays);
            double elapsedDays = Math.Clamp((currentDate - start).TotalDays, 0, totalDays);
            int shouldHaveApplied = (int)Math.Round(finances.MembershipCampaignTotalGain * elapsedDays / totalDays);
            int increment = shouldHaveApplied - finances.MembershipCampaignAppliedGain;

            if (increment > 0)
            {
                finances.ClubMembers = Math.Min(finances.ClubMembers + increment, TierMemberHardCap[team.LeagueTier - 1]);
                finances.MembershipCampaignAppliedGain += increment;
            }

            if (currentDate >= end)
            {
                finances.MembershipCampaignStartDate = null;
                finances.MembershipCampaignEndDate = null;
                finances.MembershipCampaignTotalGain = 0;
                finances.MembershipCampaignAppliedGain = 0;
            }

            return increment;
        }
    }
}
