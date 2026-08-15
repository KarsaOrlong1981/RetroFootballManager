using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // AI counterpart to stadium upgrades (StadiumViewModel) and the staff market (StaffViewModel):
    // COM teams periodically buy an affordable random upgrade and fill missing core coaching
    // roles when they have enough budget.
    public static class ClubManagementAiService
    {
        private static readonly StadiumUpgradeKind[] UpgradeKinds =
        [
            StadiumUpgradeKind.Comfort, StadiumUpgradeKind.Catering,
            StadiumUpgradeKind.Merchandise, StadiumUpgradeKind.Infrastructure,
        ];

        private static readonly EmployeeType[] CoreStaffRoles =
            [EmployeeType.AssistantCoach, EmployeeType.FitnessCoach, EmployeeType.GoalkeeperCoach];

        // Applies a random affordable upgrade (balance must still cover the upgrade cost
        // afterwards, so the team doesn't go negative). Returns true if an upgrade was made.
        public static bool TryUpgradeStadium(Team team, Random rng)
        {
            if (team.Stadium is null || team.Finances is null)
                return false;

            var affordable = UpgradeKinds
                .Select(kind => (Kind: kind, Cost: StadiumService.GetLevelUpgradeCost(team.Stadium, kind).Amount))
                .Where(c => team.Finances.CurrentBalance > c.Cost * 2)
                .OrderBy(_ => rng.Next())
                .ToList();

            if (affordable.Count == 0)
                return false;

            var choice = affordable[0];
            return StadiumService.TryApplyUpgrade(team, s => StadiumService.ApplyLevelUpgrade(s, choice.Kind), choice.Cost);
        }

        // Fills the first missing core role (assistant/fitness/goalkeeper coach) if budget allows.
        // Never fires existing staff - pure gap-filling.
        public static async Task<Employee?> TryHireMissingStaffAsync(
            Team team, StaffMarketService staffMarket, DateTime hireDate, Random rng)
        {
            if (team.Finances is null || !FinanceService.HasSpendableBalance(team))
                return null;

            // FirstOrDefault returns default(EmployeeType) = Scout if no core role is missing -
            // Scout itself is never part of CoreStaffRoles, so this unambiguously means "nothing missing".
            var missingRole = CoreStaffRoles.FirstOrDefault(role => team.Employees.All(e => e.EmployeeType != role));
            if (missingRole == default)
                return null;

            // Larger candidate pool so the needed role is likely present - if the AI finds no
            // match this week, it just retries next tick (no blocker, just a delay).
            var candidates = staffMarket.GenerateCandidates(team.LeagueTier, count: 20)
                .Where(c => c.EmployeeType == missingRole)
                .OrderByDescending(c => c.Rating)
                .ToList();
            if (candidates.Count == 0)
                return null;

            var best = candidates[0];
            if (team.Finances.CurrentBalance < best.MarketValue * 1.5)
                return null;

            await staffMarket.HireAsync(team, best, hireDate);
            return best;
        }
    }
}
