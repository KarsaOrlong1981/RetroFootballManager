using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class SeasonProgressionTests : IDisposable
    {
        private readonly string _careerPath;

        public SeasonProgressionTests()
        {
            _careerPath = Path.Combine(Path.GetTempPath(), $"rfm_season_{Guid.NewGuid():N}.json");
        }

        public void Dispose()
        {
            if (File.Exists(_careerPath))
                File.Delete(_careerPath);
        }

        private const int TeamsPerLeague = 8;

        // Builds 4 leagues of 8 teams (ids 1..32). Within each league the lower id always
        // wins, so the final table is exactly the id order (lowest id = champion).
        private static (List<Team> Teams, List<Fixture> Fixtures) BuildUniverse(int season)
        {
            var teams = new List<Team>();
            var fixtures = new List<Fixture>();

            for (int tier = 1; tier <= 4; tier++)
            {
                var ids = new List<int>();
                for (int i = 0; i < TeamsPerLeague; i++)
                {
                    int id = (tier - 1) * TeamsPerLeague + i + 1;
                    ids.Add(id);
                    teams.Add(new Team { Id = id, Name = $"T{id}", LeagueTier = tier });
                }

                var tierFixtures = FixtureGenerator.GenerateLeagueFixtures(
                    ids, season, tier, new DateTime(2026, 8, 1));

                foreach (var f in tierFixtures)
                {
                    // Lower id is the stronger team and wins.
                    if (f.HomeTeamId < f.AwayTeamId) { f.HomeGoals = 2; f.AwayGoals = 0; }
                    else { f.HomeGoals = 0; f.AwayGoals = 2; }
                    f.Played = true;
                }
                fixtures.AddRange(tierFixtures);
            }

            return (teams, fixtures);
        }

        [Fact]
        public void PromotionAndRelegation_FollowLeagueRules()
        {
            var (teams, fixtures) = BuildUniverse(season: 1);
            var career = new CareerService(_careerPath);

            // Manager team is the champion of Liga 4 (id 25).
            SeasonProgressionService.EndSeason(1, teams, fixtures, managerTeamId: 25, career);

            Team Team(int id) => teams.Single(t => t.Id == id);

            // Liga 4 top 3 (25,26,27) promoted to Liga 3.
            Assert.Equal(3, Team(25).LeagueTier);
            Assert.Equal(3, Team(27).LeagueTier);
            // Liga 4 bottom stays in Liga 4 (no relegation from the lowest tier).
            Assert.Equal(4, Team(32).LeagueTier);

            // Liga 1 champion stays up; Liga 1 bottom 3 (6,7,8) relegated to Liga 2.
            Assert.Equal(1, Team(1).LeagueTier);
            Assert.Equal(2, Team(8).LeagueTier);
        }

        [Fact]
        public void ChampionPromotion_AwardsCareerPointsAndUnlocksNextTier()
        {
            var (teams, fixtures) = BuildUniverse(season: 1);
            var career = new CareerService(_careerPath);

            var result = SeasonProgressionService.EndSeason(1, teams, fixtures, managerTeamId: 25, career);

            // Season completed (25) + champion (50) + promotion (100) = 175.
            Assert.Equal(175, result.PointsAwarded);
            Assert.Equal(175, career.Points);
            Assert.Equal(1, result.ManagerFinalPosition);
            Assert.Contains("Aufstieg", result.ManagerOutcome);
            Assert.Contains("Meister", result.ManagerOutcome);
            Assert.True(career.IsTierUnlocked(3));
        }

        [Fact]
        public void MidTableManager_GetsSurvivalOrTopSixPoints()
        {
            var (teams, fixtures) = BuildUniverse(season: 1);
            var career = new CareerService(_careerPath);

            // Team id 28 finishes 4th in Liga 4 (top six, not promoted).
            var result = SeasonProgressionService.EndSeason(1, teams, fixtures, managerTeamId: 28, career);

            Assert.Equal(4, result.ManagerFinalPosition);
            // 25 (season) + 20 (top 6) = 45.
            Assert.Equal(45, result.PointsAwarded);
        }
    }
}
