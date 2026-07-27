using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class PlayerGeneratorTests
    {
        [Fact]
        public void GenerateSquad_ReturnsRequestedSize()
        {
            var squad = PlayerGenerator.GenerateSquad(Nationality.Germany, targetAverageRating: 70, random: new Random(1));

            Assert.Equal(25, squad.Count);
        }

        [Theory]
        [InlineData(95)]
        [InlineData(70)]
        [InlineData(45)]
        public void GenerateSquad_AverageRatingConvergesNearTarget(double target)
        {
            var squad = PlayerGenerator.GenerateSquad(Nationality.Brazil, targetAverageRating: target, random: new Random(42));

            double average = squad.Average(p => p.Rating);
            Assert.InRange(average, target - 8, target + 8);
        }

        [Fact]
        public void GenerateSquad_PlayersAreNotAllIdentical()
        {
            var squad = PlayerGenerator.GenerateSquad(Nationality.England, targetAverageRating: 80, random: new Random(5));

            Assert.True(squad.Select(p => p.OffensivePower).Distinct().Count() > 1);
            Assert.True(squad.Select(p => p.Rating).Distinct().Count() > 1);
        }

        [Fact]
        public void GenerateSquad_ContainsExpectedPositionDistribution()
        {
            var squad = PlayerGenerator.GenerateSquad(Nationality.Italy, targetAverageRating: 65, random: new Random(3));

            Assert.Equal(2, squad.Count(p => p.Position == Position.Goalkeeper));
            Assert.Equal(2, squad.Count(p => p.Position == Position.Forward));
            Assert.Equal(2, squad.Count(p => p.Position == Position.CentralOffenseMidfielder));
        }

        [Fact]
        public void GenerateSquad_CrossingAccuracyIsWithinValidRangeAndHigherForWingBacks()
        {
            var squad = PlayerGenerator.GenerateSquad(Nationality.Spain, targetAverageRating: 70, random: new Random(21));

            Assert.All(squad, p => Assert.InRange(p.CrossingAccuracy, 1, 99));

            var wingBacks = squad.Where(p => p.Position is Position.LeftWingBack or Position.RightWingBack).ToList();
            var centralDefenders = squad.Where(p => p.Position == Position.CentralDefender).ToList();
            Assert.NotEmpty(wingBacks);
            Assert.NotEmpty(centralDefenders);
            Assert.True(wingBacks.Average(p => p.CrossingAccuracy) > centralDefenders.Average(p => p.CrossingAccuracy));
        }

        [Fact]
        public void GenerateSquad_ForwardsHaveHigherOffensiveThanDefensivePower()
        {
            var squad = PlayerGenerator.GenerateSquad(Nationality.Argentina, targetAverageRating: 70, random: new Random(9));

            var forwards = squad.Where(p => p.Position == Position.Forward).ToList();
            Assert.NotEmpty(forwards);
            Assert.All(forwards, f => Assert.True(f.OffensivePower > f.DefensivePower));
        }

        [Fact]
        public void GenerateSquad_CentralDefendersHaveHigherDefensiveThanOffensivePower()
        {
            var squad = PlayerGenerator.GenerateSquad(Nationality.Germany, targetAverageRating: 70, random: new Random(11));

            var defenders = squad.Where(p => p.Position == Position.CentralDefender).ToList();
            Assert.NotEmpty(defenders);
            Assert.All(defenders, d => Assert.True(d.DefensivePower > d.OffensivePower));
        }

        [Fact]
        public void GenerateSquad_MostlyMatchesPrimaryNationality_ButIncludesSomeForeigners()
        {
            var squad = PlayerGenerator.GenerateSquad(Nationality.Japan, targetAverageRating: 65, random: new Random(13));

            Assert.True(squad.Count(p => p.Nationality == Nationality.Japan) > squad.Count / 2);
        }

        [Fact]
        public void GenerateSquad_GoalkeepersGetGkAttributes_OutfieldersStayAtZero()
        {
            var squad = PlayerGenerator.GenerateSquad(Nationality.Netherlands, targetAverageRating: 70, random: new Random(17));

            var keepers = squad.Where(p => p.Position == Position.Goalkeeper).ToList();
            var outfielders = squad.Where(p => p.Position != Position.Goalkeeper).ToList();

            Assert.NotEmpty(keepers);
            Assert.All(keepers, k =>
            {
                Assert.InRange(k.GkReflexes, 1, 99);
                Assert.InRange(k.GkHandling, 1, 99);
                Assert.InRange(k.GkOneOnOne, 1, 99);
                Assert.InRange(k.GkDistribution, 1, 99);
                Assert.InRange(k.GkAerialControl, 1, 99);
            });
            Assert.All(outfielders, o => Assert.Equal(0, o.GkReflexes));
        }

        [Fact]
        public void BackfillGoalkeeperAttributesIfMissing_FillsLegacyZeroKeeper_ButLeavesOutfieldAlone()
        {
            var legacyKeeper = new Player { Position = Position.Goalkeeper, Rating = 72, Id = 5 };
            PlayerGenerator.BackfillGoalkeeperAttributesIfMissing(legacyKeeper, new Random(1));

            Assert.InRange(legacyKeeper.GkReflexes, 1, 99);
            Assert.InRange(legacyKeeper.GkHandling, 1, 99);
            Assert.InRange(legacyKeeper.GkOneOnOne, 1, 99);
            Assert.InRange(legacyKeeper.GkDistribution, 1, 99);
            Assert.InRange(legacyKeeper.GkAerialControl, 1, 99);

            var outfielder = new Player { Position = Position.Forward, Rating = 72, Id = 6 };
            PlayerGenerator.BackfillGoalkeeperAttributesIfMissing(outfielder, new Random(1));
            Assert.Equal(0, outfielder.GkReflexes);
        }

        [Fact]
        public void BackfillGoalkeeperAttributesIfMissing_DoesNotOverwriteAlreadyGeneratedKeeper()
        {
            var keeper = new Player { Position = Position.Goalkeeper, Rating = 72, GkReflexes = 77 };
            PlayerGenerator.BackfillGoalkeeperAttributesIfMissing(keeper, new Random(2));

            Assert.Equal(77, keeper.GkReflexes);
        }

        [Fact]
        public void GenerateSquad_HeaderAndJumping_HigherForCentralDefendersAndForwards_ZeroForGoalkeepers()
        {
            var squad = PlayerGenerator.GenerateSquad(Nationality.Germany, targetAverageRating: 70, random: new Random(23));

            var aerialPositions = squad.Where(p => p.Position is Position.CentralDefender or Position.Forward).ToList();
            var midfielders = squad.Where(p => p.Position == Position.CentralMidfielder).ToList();
            var keepers = squad.Where(p => p.Position == Position.Goalkeeper).ToList();

            Assert.NotEmpty(aerialPositions);
            Assert.NotEmpty(midfielders);
            Assert.True(aerialPositions.Average(p => p.HeaderStrength) > midfielders.Average(p => p.HeaderStrength));
            Assert.True(aerialPositions.Average(p => p.Jumping) > midfielders.Average(p => p.Jumping));
            Assert.All(keepers, k => Assert.Equal(0, k.HeaderStrength));
            Assert.All(keepers, k => Assert.Equal(0, k.Jumping));
        }

        [Fact]
        public void GenerateSquad_DribblingAndLongShot_HigherForMidfieldersThanCentralDefenders()
        {
            var squad = PlayerGenerator.GenerateSquad(Nationality.Germany, targetAverageRating: 70, random: new Random(29));

            var midfielders = squad.Where(p => p.Position == Position.CentralMidfielder).ToList();
            var defenders = squad.Where(p => p.Position == Position.CentralDefender).ToList();

            Assert.NotEmpty(midfielders);
            Assert.NotEmpty(defenders);
            Assert.True(midfielders.Average(p => p.Dribbling) > defenders.Average(p => p.Dribbling));
            Assert.True(midfielders.Average(p => p.LongShotAccuracy) > defenders.Average(p => p.LongShotAccuracy));
        }

        [Fact]
        public void GenerateSquad_PenaltyAndFreeKick_PreferForwardsAndMidfielders_OverDefenders()
        {
            var squad = PlayerGenerator.GenerateSquad(Nationality.Germany, targetAverageRating: 70, random: new Random(31));

            var takers = squad.Where(p => p.Position is Position.Forward or Position.CentralMidfielder).ToList();
            var defenders = squad.Where(p => p.Position == Position.CentralDefender).ToList();

            Assert.NotEmpty(takers);
            Assert.NotEmpty(defenders);
            Assert.True(takers.Average(p => p.PenaltyKick) > defenders.Average(p => p.PenaltyKick));
            Assert.True(takers.Average(p => p.FreeKick) > defenders.Average(p => p.FreeKick));
        }

        [Fact]
        public void GetRandomName_UnknownFallsBackToInternationalPool()
        {
            var (first, last) = NameBank.GetRandomName((Nationality)999, new Random(1));

            Assert.False(string.IsNullOrWhiteSpace(first));
            Assert.False(string.IsNullOrWhiteSpace(last));
        }
    }
}
