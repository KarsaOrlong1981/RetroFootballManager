using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class MatchRatingCalculatorTests
    {
        [Fact]
        public void Calculate_NoStats_ReturnsBaseRating()
        {
            var stats = new PlayerStats();
            double rating = MatchRatingCalculator.Calculate(stats, Position.CentralMidfielder, 90);
            Assert.Equal(3.5, rating, 2);
        }

        [Fact]
        public void Calculate_HattrickWithHighPassAccuracy_ClampsToBestGrade()
        {
            var stats = new PlayerStats
            {
                Goals = 3,
                Passes = 40,
                SuccessfulPasses = 37, // 92.5%
            };
            double rating = MatchRatingCalculator.Calculate(stats, Position.Forward, 90);
            Assert.Equal(1.0, rating);
        }

        [Fact]
        public void Calculate_GoalkeeperWithFoulsCardsAndManyGoalsConceded_ClampsToWorstGrade()
        {
            var stats = new PlayerStats
            {
                Fouls = 3,
                YellowCards = 1,
                RedCards = 1,
                GoalsConceded = 5,
            };
            double rating = MatchRatingCalculator.Calculate(stats, Position.Goalkeeper, 90);
            Assert.Equal(6.0, rating);
        }

        [Fact]
        public void Calculate_ShortCameoAppearance_IsDampenedTowardBaseRating()
        {
            var stats = new PlayerStats { Fouls = 3, YellowCards = 1, RedCards = 1 };
            double fullMatchRating = MatchRatingCalculator.Calculate(stats, Position.CentralMidfielder, 90);
            double cameoRating = MatchRatingCalculator.Calculate(stats, Position.CentralMidfielder, 1);

            Assert.True(cameoRating < fullMatchRating);
            Assert.True(cameoRating > 3.5);
        }

        [Fact]
        public void Calculate_BelowMinimumPassSample_IgnoresPassAccuracy()
        {
            var lowSample = new PlayerStats { Passes = 2, SuccessfulPasses = 2 };
            var noPasses = new PlayerStats();

            double lowSampleRating = MatchRatingCalculator.Calculate(lowSample, Position.CentralMidfielder, 90);
            double noPassesRating = MatchRatingCalculator.Calculate(noPasses, Position.CentralMidfielder, 90);

            Assert.Equal(noPassesRating, lowSampleRating, 5);
        }

        [Fact]
        public void Calculate_GoalkeeperCleanSheet_GetsBonusOverConcedingGoalkeeper()
        {
            var cleanSheet = new PlayerStats { GoalsConceded = 0 };
            var conceded = new PlayerStats { GoalsConceded = 1 };

            double cleanSheetRating = MatchRatingCalculator.Calculate(cleanSheet, Position.Goalkeeper, 90);
            double concededRating = MatchRatingCalculator.Calculate(conceded, Position.Goalkeeper, 90);

            Assert.True(cleanSheetRating < concededRating);
        }

        [Fact]
        public void Calculate_GoalBonus_VariesByPosition()
        {
            var stats = new PlayerStats { Goals = 1 };

            double forwardRating = MatchRatingCalculator.Calculate(stats, Position.Forward, 90);
            double defenderRating = MatchRatingCalculator.Calculate(stats, Position.CentralDefender, 90);

            Assert.True(defenderRating < forwardRating);
        }

        [Fact]
        public void Calculate_HighDuelAndHeaderWinRate_ImprovesRatingOverPlayerWithoutThem()
        {
            var withDuels = new PlayerStats
            {
                Tackles = 6,
                TacklesWon = 5,
                HeaderDuels = 6,
                HeaderDuelsWon = 5,
            };
            var withoutDuels = new PlayerStats();

            double withDuelsRating = MatchRatingCalculator.Calculate(withDuels, Position.CentralDefender, 90);
            double withoutDuelsRating = MatchRatingCalculator.Calculate(withoutDuels, Position.CentralDefender, 90);

            Assert.True(withDuelsRating < withoutDuelsRating);
        }

        [Fact]
        public void Calculate_MoreOffsides_WorsensRatingComparedToNone()
        {
            var withOffsides = new PlayerStats { Offsides = 3 };
            var withoutOffsides = new PlayerStats();

            double withOffsidesRating = MatchRatingCalculator.Calculate(withOffsides, Position.Forward, 90);
            double withoutOffsidesRating = MatchRatingCalculator.Calculate(withoutOffsides, Position.Forward, 90);

            Assert.True(withOffsidesRating > withoutOffsidesRating);
        }

        [Fact]
        public void Calculate_ResultIsAlwaysClampedBetweenOneAndSix()
        {
            var extremeGood = new PlayerStats { Goals = 10, Passes = 20, SuccessfulPasses = 20 };
            var extremeBad = new PlayerStats { Fouls = 20, YellowCards = 3, RedCards = 3, GoalsConceded = 15 };

            Assert.InRange(MatchRatingCalculator.Calculate(extremeGood, Position.Forward, 90), 1.0, 6.0);
            Assert.InRange(MatchRatingCalculator.Calculate(extremeBad, Position.Goalkeeper, 90), 1.0, 6.0);
        }
    }
}
