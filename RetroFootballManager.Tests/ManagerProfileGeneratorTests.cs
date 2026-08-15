using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class ManagerProfileGeneratorTests
    {
        private static readonly DateTime RefDate = new(2026, 8, 1);

        [Theory]
        [InlineData(4, CoachingLicense.C, 3, 1)]
        [InlineData(3, CoachingLicense.B, 5, 1)]
        [InlineData(2, CoachingLicense.A, 7, 2)]
        [InlineData(1, CoachingLicense.Pro, 10, 3)]
        public void GetBudget_ReturnsExpectedLicenseAndCeiling(
            int tier, CoachingLicense expectedLicense, int expectedCeiling, int expectedFloor)
        {
            var (license, ceiling, floor) = ManagerProfileGenerator.GetBudget(tier);
            Assert.Equal(expectedLicense, license);
            Assert.Equal(expectedCeiling, ceiling);
            Assert.Equal(expectedFloor, floor);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void Generate_NeverHasAllFiveSkillsAtCeiling(int tier)
        {
            var (_, ceiling, _) = ManagerProfileGenerator.GetBudget(tier);
            var rng = new Random(42);

            for (int i = 0; i < 50; i++)
            {
                var profile = ManagerProfileGenerator.Generate(tier, Nationality.Germany, rng, RefDate);
                int[] skills = [
                    profile.TrainingDesign, profile.Motivation, profile.OffensiveCreation,
                    profile.DefensiveOrganization, profile.InGameCoaching
                ];
                int atCeiling = skills.Count(s => s == ceiling);
                Assert.Equal(2, atCeiling);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void Generate_AllSkillsWithinFloorAndCeiling(int tier)
        {
            var (_, ceiling, floor) = ManagerProfileGenerator.GetBudget(tier);
            var rng = new Random(7);

            var profile = ManagerProfileGenerator.Generate(tier, Nationality.Germany, rng, RefDate);
            int[] skills = [
                profile.TrainingDesign, profile.Motivation, profile.OffensiveCreation,
                profile.DefensiveOrganization, profile.InGameCoaching
            ];
            Assert.All(skills, s => Assert.InRange(s, floor, ceiling));
        }

        [Fact]
        public void Generate_Tier1CoachesAreStatisticallyStrongerThanTier4()
        {
            var rng = new Random(123);
            double SumSkills(ManagerProfile p) =>
                p.TrainingDesign + p.Motivation + p.OffensiveCreation + p.DefensiveOrganization + p.InGameCoaching;

            double tier1Avg = Enumerable.Range(0, 100)
                .Select(_ => SumSkills(ManagerProfileGenerator.Generate(1, Nationality.Germany, rng, RefDate)))
                .Average();
            double tier4Avg = Enumerable.Range(0, 100)
                .Select(_ => SumSkills(ManagerProfileGenerator.Generate(4, Nationality.Germany, rng, RefDate)))
                .Average();

            Assert.True(tier1Avg > tier4Avg, $"tier1Avg={tier1Avg}, tier4Avg={tier4Avg}");
        }

        [Fact]
        public void Generate_AssignsExpectedLicenseAndZeroUnspentPoints()
        {
            var rng = new Random(1);
            var profile = ManagerProfileGenerator.Generate(2, Nationality.Germany, rng, RefDate);

            Assert.Equal(CoachingLicense.A, profile.License);
            Assert.Equal(0, profile.UnspentSkillPoints);
            Assert.False(profile.IsHuman);
            Assert.False(string.IsNullOrWhiteSpace(profile.FirstName));
            Assert.False(string.IsNullOrWhiteSpace(profile.LastName));
        }

        [Fact]
        public void CreateUniverse_EveryTeamGetsAManagerProfileMatchingItsTier()
        {
            var (_, teams) = UniverseGenerator.CreateUniverse(season: 1, random: new Random(5));

            Assert.All(teams, team =>
            {
                Assert.NotNull(team.ManagerProfile);
                var (expectedLicense, _, _) = ManagerProfileGenerator.GetBudget(team.LeagueTier);
                Assert.Equal(expectedLicense, team.ManagerProfile!.License);
            });
        }
    }
}
