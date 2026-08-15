using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Generates manager profiles (AI teams here; the human's own profile is built in the
    // "create your manager" flow, Phase 10d, reusing the same budget table). License and
    // skill budget are directly derived from the league tier a career has unlocked
    // (CareerService.HighestUnlockedTier) - no separate currency.
    public static class ManagerProfileGenerator
    {
        // tier -> (license, ceiling for the 2 "hero" skills, floor/cap for the other 3).
        // Never all 5 skills at ceiling - exactly 2 reach it, the rest are capped at
        // Ceiling-3 (never below Floor).
        private static readonly Dictionary<int, (CoachingLicense License, int Ceiling, int Floor)> TierBudget = new()
        {
            [4] = (CoachingLicense.C, 3, 1),
            [3] = (CoachingLicense.B, 5, 1),
            [2] = (CoachingLicense.A, 7, 2),
            [1] = (CoachingLicense.Pro, 10, 3),
        };

        public static (CoachingLicense License, int Ceiling, int Floor) GetBudget(int tier) =>
            TierBudget[Math.Clamp(tier, 1, 4)];

        // Reverse lookup for the "Punkte verteilen" flow (spending a profile's leftover
        // UnspentSkillPoints after creation) - a profile's License is fixed once created, so
        // its own ceiling/floor stay whatever tier it was generated under, independent of
        // whatever tier the career has since unlocked.
        public static (int Ceiling, int Floor) GetBudgetForLicense(CoachingLicense license) =>
            license switch
            {
                CoachingLicense.C => (3, 1),
                CoachingLicense.B => (5, 1),
                CoachingLicense.A => (7, 2),
                CoachingLicense.Pro => (10, 3),
                _ => (10, 3),
            };

        // Total skill points a fresh profile at this tier is built from - exactly 2 skills
        // at Ceiling, 3 at Floor (the human creation UI distributes this same pool freely
        // between Floor and Ceiling per skill instead of pre-assigning it).
        public static int GetSkillPointBudget(int tier)
        {
            var (_, ceiling, floor) = GetBudget(tier);
            return ceiling * 2 + floor * 3;
        }

        public static ManagerProfile Generate(
            int tier, Nationality nationality, Random random, DateTime referenceDate, bool isHuman = false)
        {
            var rng = random;
            var (license, ceiling, floor) = GetBudget(tier);
            var (firstName, lastName) = NameBank.GetRandomName(nationality, rng);

            int nonHeroMax = Math.Max(floor, ceiling - 3);
            var heroIndices = Enumerable.Range(0, 5).OrderBy(_ => rng.Next()).Take(2).ToHashSet();
            var skills = new int[5];
            for (int i = 0; i < 5; i++)
                skills[i] = heroIndices.Contains(i) ? ceiling : rng.Next(floor, nonHeroMax + 1);

            return new ManagerProfile
            {
                IsHuman = isHuman,
                FirstName = firstName,
                LastName = lastName,
                BirthDate = referenceDate.AddYears(-rng.Next(38, 66)),
                License = license,
                TrainingDesign = skills[0],
                Motivation = skills[1],
                OffensiveCreation = skills[2],
                DefensiveOrganization = skills[3],
                InGameCoaching = skills[4],
                UnspentSkillPoints = 0,
            };
        }
    }
}
