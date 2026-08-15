using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Turns a manager's license + one of his 5 skills (1-10) into a single multiplier, fed
    // additively into the existing systems below (training/talks/team strength/in-match
    // attributes) alongside their current factors - none of those formulas are restructured.
    // Mirrors PersonalityEffects/InMatchCharacterEffects: an explicit neutral (1.0) fallback
    // for a null profile so every call site works before a career even has one.
    public static class ManagerEffects
    {
        private static double LicenseMultiplier(CoachingLicense license) => license switch
        {
            CoachingLicense.C => 0.85,
            CoachingLicense.B => 1.0,
            CoachingLicense.A => 1.15,
            CoachingLicense.Pro => 1.3,
            _ => 1.0,
        };

        // +/-30% band around the 1-10 skill range's midpoint (5.5) - a skill of 10 is
        // noticeably stronger than a 1, but the license multiplier above still decides how
        // much that skill actually counts (a Pro's "7" outperforms a C's "7").
        private static double SkillFactor(int skill) => 1.0 + ((skill - 5.5) / 4.5) * 0.3;

        private static double Factor(ManagerProfile? manager, Func<ManagerProfile, int> skill) =>
            manager is null ? 1.0 : LicenseMultiplier(manager.License) * SkillFactor(skill(manager));

        public static double TrainingDesignFactor(ManagerProfile? manager) => Factor(manager, m => m.TrainingDesign);
        public static double MotivationFactor(ManagerProfile? manager) => Factor(manager, m => m.Motivation);
        public static double OffensiveCreationFactor(ManagerProfile? manager) => Factor(manager, m => m.OffensiveCreation);
        public static double DefensiveOrganizationFactor(ManagerProfile? manager) => Factor(manager, m => m.DefensiveOrganization);
        public static double InGameCoachingFactor(ManagerProfile? manager) => Factor(manager, m => m.InGameCoaching);

        // Annual salary: a license base scaled by total skill quality - mirrors
        // StaffGenerator's Salary = rating * 1200 pattern (better rating -> higher pay), here
        // "rating" is the sum of all 5 skills (5-50) instead of a single averaged value.
        private static double LicenseBaseSalary(CoachingLicense license) => license switch
        {
            CoachingLicense.C => 30_000,
            CoachingLicense.B => 60_000,
            CoachingLicense.A => 120_000,
            CoachingLicense.Pro => 220_000,
            _ => 30_000,
        };

        public static int AnnualSalary(ManagerProfile manager)
        {
            int skillSum = manager.TrainingDesign + manager.Motivation + manager.OffensiveCreation
                + manager.DefensiveOrganization + manager.InGameCoaching;
            return (int)Math.Round(LicenseBaseSalary(manager.License) * (skillSum / 27.5));
        }
    }
}
