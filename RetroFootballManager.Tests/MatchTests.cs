using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class MatchTests
    {
        [Fact]
        public void Simulate_ProducesConsistentGoalsAndScorers()
        {
            var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
            var away = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
            var match = new Match(home, away, new Random(42));

            var result = match.Simulate();

            Assert.Equal(result.HomeGoals, result.HomeScorers.Count);
            Assert.Equal(result.AwayGoals, result.AwayScorers.Count);
            Assert.True(result.MatchStatsHome.Shots >= result.MatchStatsHome.ShotsOnTarget);
            Assert.True(result.MatchStatsHome.ShotsOnTarget >= result.MatchStatsHome.Goals);
            Assert.InRange(result.MatchStatsHome.Possession + result.MatchStatsAway.Possession, 99, 101);
        }

        [Fact]
        public void Simulate_TeamWithGoodFitnessCoach_LosesLessFitnessThanUncoachedTeam()
        {
            var coached = TestHelpers.CreateTeam("Coached FC", baseRating: 65);
            coached.Employees.Add(new Employee { EmployeeType = EmployeeType.FitnessCoach, FitnessTraining = 90 });
            var plain = TestHelpers.CreateTeam("Plain FC", baseRating: 65);
            var away1 = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
            var away2 = TestHelpers.CreateTeam("Gast FC", baseRating: 65);

            new Match(coached, away1, new Random(21)).Simulate();
            new Match(plain, away2, new Random(21)).Simulate();

            double coachedAvgFitness = coached.Players.Average(p => p.Fitness);
            double plainAvgFitness = plain.Players.Average(p => p.Fitness);
            Assert.True(coachedAvgFitness >= plainAvgFitness);
        }

        [Fact]
        public void Simulate_IsDeterministic_WithSameSeed()
        {
            var homeA = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
            var awayA = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
            var resultA = new Match(homeA, awayA, new Random(7)).Simulate();

            var homeB = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
            var awayB = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
            var resultB = new Match(homeB, awayB, new Random(7)).Simulate();

            Assert.Equal(resultA.HomeGoals, resultB.HomeGoals);
            Assert.Equal(resultA.AwayGoals, resultB.AwayGoals);
            Assert.Equal(resultA.Events.Count, resultB.Events.Count);
        }

        [Fact]
        public void StrongerTeam_WinsMoreOftenOverManySimulations()
        {
            int strongWins = 0, weakWins = 0;
            var random = new Random(123);

            for (int i = 0; i < 150; i++)
            {
                var strong = TestHelpers.CreateTeam("Stark", baseRating: 85);
                var weak = TestHelpers.CreateTeam("Schwach", baseRating: 35);
                var result = new Match(strong, weak, random).Simulate();

                if (result.HomeGoals > result.AwayGoals) strongWins++;
                else if (result.AwayGoals > result.HomeGoals) weakWins++;
            }

            Assert.True(strongWins > weakWins);
        }

        [Fact]
        public void Simulate_DecaysPlayerFitness()
        {
            var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65, fitness: 95);
            var away = TestHelpers.CreateTeam("Gast FC", baseRating: 65, fitness: 95);

            new Match(home, away, new Random(1)).Simulate();

            Assert.All(home.Players, p => Assert.True(p.Fitness < 95));
        }

        [Fact]
        public void Simulate_OverManyMatches_SometimesAwardsPenalties()
        {
            int totalPenaltyEvents = 0;
            int totalPenaltyStats = 0;
            var random = new Random(99);

            for (int i = 0; i < 60; i++)
            {
                var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
                var away = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
                var result = new Match(home, away, random).Simulate();

                totalPenaltyEvents += result.Events.Count(e => e.Type == GameEventType.Penalty);
                totalPenaltyStats += result.MatchStatsHome.Penaltys + result.MatchStatsAway.Penaltys;
            }

            Assert.True(totalPenaltyEvents > 0);
            Assert.Equal(totalPenaltyStats, totalPenaltyEvents);
        }

        [Fact]
        public void Simulate_SecondYellowCardAlwaysBecomesRedCard()
        {
            var random = new Random(55);
            bool foundSecondYellowRedCard = false;

            for (int i = 0; i < 200; i++)
            {
                var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
                var away = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
                var result = new Match(home, away, random).Simulate();

                foreach (var stats in result.PlayerMatchStats.Values)
                {
                    if (stats.YellowCards >= 2)
                        Assert.True(stats.RedCards >= 1);
                }

                if (result.Events.Any(e => e.Description.Contains("Gelb-Rot")))
                    foundSecondYellowRedCard = true;
            }

            Assert.True(foundSecondYellowRedCard);
        }

        [Fact]
        public void Simulate_OverManyMatches_RecordsAssistsWithoutExceedingGoalCount()
        {
            var random = new Random(31);
            int totalGoals = 0;
            int totalAssists = 0;

            for (int i = 0; i < 60; i++)
            {
                var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
                var away = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
                var result = new Match(home, away, random).Simulate();

                totalGoals += result.HomeGoals + result.AwayGoals;
                totalAssists += result.PlayerMatchStats.Values.Sum(s => s.Assists);
            }

            Assert.True(totalAssists > 0);
            Assert.True(totalAssists <= totalGoals);
        }

        [Fact]
        public void Simulate_StrongPressingTeam_BeatsWeakCounterAttackTeam_MostOfTheTime()
        {
            // Reproduziert das gemeldete Szenario: Bayern (88, Pressing) vs. London (48, CounterAttack).
            int strongWins = 0, weakWins = 0, draws = 0;
            int strongGoals = 0, weakGoals = 0;
            var random = new Random(2024);

            for (int i = 0; i < 40; i++)
            {
                var strong = TestHelpers.CreateTeam("Bayern", baseRating: 88, style: PlayingStyle.Pressing);
                var weak = TestHelpers.CreateTeam("London", baseRating: 48, style: PlayingStyle.CounterAttack);
                var result = new Match(strong, weak, random).Simulate();

                strongGoals += result.HomeGoals;
                weakGoals += result.AwayGoals;

                if (result.HomeGoals > result.AwayGoals) strongWins++;
                else if (result.AwayGoals > result.HomeGoals) weakWins++;
                else draws++;
            }

            Assert.True(strongWins > weakWins + draws);
            Assert.True(strongGoals > weakGoals * 2);
        }

        [Fact]
        public void Simulate_OverManyMatches_RedCardsAreRareNotEveryGame()
        {
            var random = new Random(77);
            int matchesWithAnyRedCard = 0;
            const int totalMatches = 40;

            for (int i = 0; i < totalMatches; i++)
            {
                var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
                var away = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
                var result = new Match(home, away, random).Simulate();

                if (result.MatchStatsHome.RedCards + result.MatchStatsAway.RedCards > 0)
                    matchesWithAnyRedCard++;
            }

            // Rote Karten sollen selten sein (~1 von mehreren Spielen), nicht in fast jedem Spiel.
            Assert.True(matchesWithAnyRedCard < totalMatches / 3);
        }
    }
}
