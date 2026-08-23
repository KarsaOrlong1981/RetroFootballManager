using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Player-phase (personal terms) side of a negotiation: a player's own expectations for
    // wage, squad role, contract length and exit-clause flexibility, and how well a given
    // offer matches them (0-100 satisfaction score, mapped to the same 5-tier
    // NegotiationMoodLevel used for the manager phase).
    public static class PlayerTermsExpectationService
    {
        private static readonly Dictionary<RoleInTeam, int> RoleRank = new()
        {
            [RoleInTeam.Backup] = 0,
            [RoleInTeam.FutureTalent] = 1,
            [RoleInTeam.RotationPlayer] = 2,
            [RoleInTeam.KeyPlayer] = 3,
        };

        public static double EstimateExpectedWage(Player player) => PlayerValuationService.EstimateAnnualSalary(player);

        // A player's self-image of the role they deserve, from their rating relative to the
        // squad average and their age/talent (young high-talent prospects see themselves as
        // a future key player, not as a mere rotation option).
        public static RoleInTeam EstimateExpectedRole(Player player, double squadAverageRating)
        {
            if (player.Rating >= squadAverageRating + 5)
                return RoleInTeam.KeyPlayer;
            if (player.Age <= 21 && player.Talent >= 75)
                return RoleInTeam.FutureTalent;
            if (player.Rating >= squadAverageRating - 3)
                return RoleInTeam.RotationPlayer;
            return RoleInTeam.Backup;
        }

        // Young players prioritize long-term security, established pros settle in for the
        // long haul, veterans only commit season-by-season.
        public static int EstimatePreferredContractYears(int age) => age switch
        {
            <= 23 => 4,
            <= 29 => 3,
            <= 33 => 2,
            _ => 1,
        };

        // Ambitious/talented players (or ones already unhappy) want the flexibility of an
        // exit clause; it barely matters to the rest.
        public static bool WantsExitClause(Player player) => player.Talent >= 75 || player.WantsToLeaveClub;

        public static double EstimateSatisfaction(
            Player player, double squadAverageRating, double offeredWage, RoleInTeam offeredRole,
            int offeredContractYears, bool hasExitClause, double totalAnnualBonusValue)
        {
            double expectedWage = EstimateExpectedWage(player);
            double wageScore = Math.Clamp(offeredWage / expectedWage, 0, 1.5) / 1.5 * 100;

            var expectedRole = EstimateExpectedRole(player, squadAverageRating);
            int roleDelta = RoleRank[offeredRole] - RoleRank[expectedRole];
            double roleScore = Math.Clamp(50 + roleDelta * 25, 0, 100);

            int preferredYears = EstimatePreferredContractYears(player.Age);
            double yearScore = Math.Clamp(100 - Math.Abs(offeredContractYears - preferredYears) * 15, 0, 100);

            bool wantsExitClause = WantsExitClause(player);
            double clauseScore = wantsExitClause == hasExitClause ? 100 : (wantsExitClause ? 30 : 70);

            double bonusScore = Math.Clamp(totalAnnualBonusValue / Math.Max(expectedWage * 0.1, 1) * 100, 0, 100);

            return wageScore * 0.40 + roleScore * 0.20 + yearScore * 0.10 + clauseScore * 0.15 + bonusScore * 0.10 + 50 * 0.05;
        }

        public static NegotiationMoodLevel EvaluateMood(double satisfactionScore) => satisfactionScore switch
        {
            >= 90 => NegotiationMoodLevel.Delighted,
            >= 70 => NegotiationMoodLevel.Happy,
            >= 50 => NegotiationMoodLevel.Neutral,
            >= 30 => NegotiationMoodLevel.Impatient,
            _ => NegotiationMoodLevel.Furious,
        };

        // Renewal-only pre-check: whether the player is even willing to talk before
        // financial terms are on the table - driven by morale and playing time, not money.
        public static bool IsWillingToDiscussRenewal(Player player, double squadAverageRating) =>
            !player.WantsToLeaveClub && player.Moral >= 35
            && (player.Rating >= squadAverageRating - 8 || player.Age <= 26);
    }
}
