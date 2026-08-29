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

        // Difficulty-scaled staff priority list: a fuller, better-run staff makes for a tougher
        // AI opponent, so Hard eventually covers every EmployeeType, Normal adds the
        // fitness/medical/mood roles, Easy never grows past the original 3 core coaching roles.
        // TryHireMissingStaffAsync still only fills the FIRST missing role per week, so this
        // just changes how far a healthy club's staff eventually grows, not how fast.
        private static readonly EmployeeType[] EasyStaffRoles =
            [EmployeeType.AssistantCoach, EmployeeType.FitnessCoach, EmployeeType.GoalkeeperCoach];

        private static readonly EmployeeType[] NormalStaffRoles =
            [.. EasyStaffRoles, EmployeeType.Physiotherapist, EmployeeType.MedicalStaff, EmployeeType.Psychologist];

        private static readonly EmployeeType[] HardStaffRoles =
            [.. NormalStaffRoles, EmployeeType.YouthCoach, EmployeeType.Scout, EmployeeType.DirectorOfFootball,
             EmployeeType.Analyst];

        private static EmployeeType[] StaffRolesFor(Difficulty difficulty) => difficulty switch
        {
            Difficulty.Hard => HardStaffRoles,
            Difficulty.Easy => EasyStaffRoles,
            _ => NormalStaffRoles,
        };

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

        // Fills the first missing role (from the difficulty's priority list) if budget allows.
        // Never fires existing staff - pure gap-filling.
        public static async Task<Employee?> TryHireMissingStaffAsync(
            Team team, StaffMarketService staffMarket, Difficulty difficulty, DateTime hireDate, Random rng)
        {
            if (team.Finances is null || !FinanceService.HasSpendableBalance(team))
                return null;

            var roles = StaffRolesFor(difficulty);
            var missingRole = roles.FirstOrDefault(role => team.Employees.All(e => e.EmployeeType != role), (EmployeeType)(-1));
            if ((int)missingRole == -1)
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
