using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class ForeignClubGeneratorTests
    {
        [Fact]
        public void GenerateClubs_ChampionsLeague_Produces32Clubs()
        {
            var clubs = ForeignClubGenerator.GenerateClubs(ForeignClubGenerator.Competition.ChampionsLeague, new Random(1));
            Assert.Equal(32, clubs.Count);
        }

        [Fact]
        public void GenerateClubs_NoDuplicateNames()
        {
            var clubs = ForeignClubGenerator.GenerateClubs(ForeignClubGenerator.Competition.ChampionsLeague, new Random(2));
            Assert.Equal(clubs.Count, clubs.Select(c => c.Name).Distinct().Count());
        }

        [Fact]
        public void GenerateClubs_TopTierCountries_RateHigherThanEasternEurope()
        {
            var clubs = ForeignClubGenerator.GenerateClubs(ForeignClubGenerator.Competition.ChampionsLeague, new Random(3));

            double topTierAvg = clubs.Where(c => c.Nationality == Nationality.Spain).Average(c => c.AverageRating);
            double easternEuropeAvg = clubs.Where(c => c.Nationality == Nationality.EasternEurope).Average(c => c.AverageRating);
            double smallPoolAvg = clubs.Where(c => c.Nationality == Nationality.Iceland).Average(c => c.AverageRating);

            Assert.True(topTierAvg > easternEuropeAvg);
            Assert.True(easternEuropeAvg > smallPoolAvg);
        }

        [Fact]
        public void GenerateClubs_EuropaCup_IsWeakerThanChampionsLeague()
        {
            var cl = ForeignClubGenerator.GenerateClubs(ForeignClubGenerator.Competition.ChampionsLeague, new Random(4));
            var el = ForeignClubGenerator.GenerateClubs(ForeignClubGenerator.Competition.EuropaCup, new Random(4));

            double clAvg = cl.Average(c => c.AverageRating);
            double elAvg = el.Average(c => c.AverageRating);

            Assert.True(clAvg > elAvg);
        }
    }
}
