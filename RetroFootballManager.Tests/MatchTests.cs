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
        public void Simulate_OverManyMatches_SometimesAwardsDirectFreeKicks()
        {
            int totalFreeKickEvents = 0;
            var random = new Random(99);

            for (int i = 0; i < 60; i++)
            {
                var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
                var away = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
                var result = new Match(home, away, random).Simulate();

                totalFreeKickEvents += result.Events.Count(e => e.Type == GameEventType.FreeKick);
            }

            Assert.True(totalFreeKickEvents > 0);
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

        [Fact]
        public void Simulate_AllPlayersWithMinutesPlayed_GetRatingInValidRange()
        {
            var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
            var away = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
            var result = new Match(home, away, new Random(12)).Simulate();

            foreach (var (playerId, minutes) in result.MinutesPlayed)
            {
                if (minutes <= 0)
                    continue;
                Assert.True(result.PlayerMatchStats.ContainsKey(playerId));
                Assert.InRange(result.PlayerMatchStats[playerId].Rating, 1.0, 6.0);
            }
        }

        [Fact]
        public void Simulate_OverManyMatches_ProducesPassingCrossingDuelAndHeaderEvents()
        {
            var random = new Random(88);
            int totalPasses = 0, totalCrosses = 0, totalTackles = 0, totalHeaderDuels = 0, totalSaves = 0;

            for (int i = 0; i < 20; i++)
            {
                var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
                var away = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
                var result = new Match(home, away, random).Simulate();

                foreach (var stats in result.PlayerMatchStats.Values)
                {
                    totalPasses += stats.Passes;
                    totalCrosses += stats.Crosses;
                    totalTackles += stats.Tackles;
                    totalHeaderDuels += stats.HeaderDuels;
                    totalSaves += stats.Saves;
                }
            }

            Assert.True(totalPasses > 0);
            Assert.True(totalCrosses > 0);
            Assert.True(totalTackles > 0);
            Assert.True(totalHeaderDuels > 0);
            Assert.True(totalSaves > 0);
        }

        [Fact]
        public void Simulate_DuelWinnerCounts_NeverExceedTotalDuels()
        {
            var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
            var away = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
            var result = new Match(home, away, new Random(3)).Simulate();

            int totalTackles = result.PlayerMatchStats.Values.Sum(s => s.Tackles);
            int totalTacklesWon = result.PlayerMatchStats.Values.Sum(s => s.TacklesWon);
            int totalHeaderDuels = result.PlayerMatchStats.Values.Sum(s => s.HeaderDuels);
            int totalHeaderDuelsWon = result.PlayerMatchStats.Values.Sum(s => s.HeaderDuelsWon);

            Assert.True(totalTacklesWon <= totalTackles);
            Assert.True(totalHeaderDuelsWon <= totalHeaderDuels);
        }

        [Fact]
        public void Simulate_OverManyMatches_ForwardsAreSometimesCaughtOffside()
        {
            var random = new Random(64);
            int totalOffsides = 0;

            for (int i = 0; i < 40; i++)
            {
                var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
                var away = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
                var result = new Match(home, away, random).Simulate();

                totalOffsides += result.MatchStatsHome.Offsides + result.MatchStatsAway.Offsides;
            }

            Assert.True(totalOffsides > 0);
        }

        [Fact]
        public void Simulate_ForwardsWithPoorPositioning_AreCaughtOffsideMoreOftenThanGoodPositioning()
        {
            var random = new Random(65);
            int offsidesPoorPositioning = 0;
            int offsidesGoodPositioning = 0;
            const int matches = 60;

            for (int i = 0; i < matches; i++)
            {
                var poorTeam = TestHelpers.CreateTeam("Schlecht positioniert", baseRating: 65);
                foreach (var p in poorTeam.Players.Where(p => p.Position == Position.Forward))
                    p.Positioning = 5;
                var defenders1 = TestHelpers.CreateTeam("Abwehr1", baseRating: 65);
                offsidesPoorPositioning += new Match(poorTeam, defenders1, random).Simulate().MatchStatsHome.Offsides;

                var goodTeam = TestHelpers.CreateTeam("Gut positioniert", baseRating: 65);
                foreach (var p in goodTeam.Players.Where(p => p.Position == Position.Forward))
                    p.Positioning = 95;
                var defenders2 = TestHelpers.CreateTeam("Abwehr2", baseRating: 65);
                offsidesGoodPositioning += new Match(goodTeam, defenders2, random).Simulate().MatchStatsHome.Offsides;
            }

            Assert.True(offsidesPoorPositioning > offsidesGoodPositioning,
                $"poor={offsidesPoorPositioning}, good={offsidesGoodPositioning}");
        }

        [Fact]
        public void Simulate_DefendersWithPoorPositioning_LoseMoreHeaderDuelsThanGoodPositioning()
        {
            var random = new Random(66);
            int headerDuelsWonPoor = 0, headerDuelsTotalPoor = 0;
            int headerDuelsWonGood = 0, headerDuelsTotalGood = 0;
            const int matches = 60;

            for (int i = 0; i < matches; i++)
            {
                var attackers = TestHelpers.CreateTeam("Angriff", baseRating: 65);

                var poorTeam = TestHelpers.CreateTeam("Schlechte Abwehr", baseRating: 65);
                foreach (var p in poorTeam.Players.Where(p => p.Position == Position.CentralDefender))
                {
                    p.Positioning = 5;
                    // BestHeaderDefender picks by raw HeaderPower (HeaderStrength/Jumping/Size),
                    // not by position - every TestHelpers player ties on those by default, so
                    // without a clear edge here the tie-break (list order) can silently pick a
                    // DIFFERENT, unmodified player and the Positioning manipulation below never
                    // actually reaches the code path being measured.
                    p.HeaderStrength = 90; p.Jumping = 90; p.Size = 1.95;
                }
                var resultPoor = new Match(attackers, poorTeam, random).Simulate();
                foreach (var stats in resultPoor.PlayerMatchStats.Values.Where(s => poorTeam.Players.Any(p => p.Id == s.PlayerId)))
                {
                    headerDuelsWonPoor += stats.HeaderDuelsWon;
                    headerDuelsTotalPoor += stats.HeaderDuels;
                }

                var goodTeam = TestHelpers.CreateTeam("Gute Abwehr", baseRating: 65);
                foreach (var p in goodTeam.Players.Where(p => p.Position == Position.CentralDefender))
                {
                    p.Positioning = 95;
                    p.HeaderStrength = 90; p.Jumping = 90; p.Size = 1.95;
                }
                var resultGood = new Match(attackers, goodTeam, random).Simulate();
                foreach (var stats in resultGood.PlayerMatchStats.Values.Where(s => goodTeam.Players.Any(p => p.Id == s.PlayerId)))
                {
                    headerDuelsWonGood += stats.HeaderDuelsWon;
                    headerDuelsTotalGood += stats.HeaderDuels;
                }
            }

            double winRatePoor = (double)headerDuelsWonPoor / Math.Max(1, headerDuelsTotalPoor);
            double winRateGood = (double)headerDuelsWonGood / Math.Max(1, headerDuelsTotalGood);
            Assert.True(winRateGood > winRatePoor, $"poor={winRatePoor}, good={winRateGood}");
        }

        [Fact]
        public void AdvanceMinute_PossessionAndPassStatsAreLiveDuringTheMatch_NotOnlyAtFullTime()
        {
            var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
            var away = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
            var match = new Match(home, away, new Random(9));

            match.Begin();
            for (int i = 0; i < 10; i++)
                match.AdvanceMinute();

            var result = match.Result;
            Assert.InRange(result.MatchStatsHome.Possession + result.MatchStatsAway.Possession, 99, 101);
            Assert.True(result.MatchStatsHome.Passes > 0 || result.MatchStatsAway.Passes > 0);
        }

        [Fact]
        public void Simulate_TikiTakaWithGoodPassingAndPositioning_GetsMorePossessionThanPressingOpponent()
        {
            var random = new Random(70);
            int homePossessionSum = 0;
            const int matches = 30;

            for (int i = 0; i < matches; i++)
            {
                var possessionTeam = TestHelpers.CreateTeam("Ballbesitz FC", baseRating: 70, style: PlayingStyle.TikiTaka);
                var pressingTeam = TestHelpers.CreateTeam("Pressing FC", baseRating: 70, style: PlayingStyle.Pressing);
                var result = new Match(possessionTeam, pressingTeam, random).Simulate();
                homePossessionSum += result.MatchStatsHome.Possession;
            }

            double avgHomePossession = (double)homePossessionSum / matches;
            Assert.True(avgHomePossession > 50, $"avgHomePossession={avgHomePossession}");
        }

        [Fact]
        public void Simulate_OpponentWithHighDuelHardness_ReducesOwnPossessionShare()
        {
            var random = new Random(71);
            int homePossessionSumSoftOpponent = 0, homePossessionSumHardOpponent = 0;
            const int matches = 30;

            for (int i = 0; i < matches; i++)
            {
                var home1 = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
                var softAway = TestHelpers.CreateTeam("Weiche Abwehr", baseRating: 65);
                foreach (var p in softAway.Players) p.DuelHardness = 10;
                homePossessionSumSoftOpponent += new Match(home1, softAway, random).Simulate().MatchStatsHome.Possession;

                var home2 = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
                var hardAway = TestHelpers.CreateTeam("Harte Abwehr", baseRating: 65);
                foreach (var p in hardAway.Players) p.DuelHardness = 95;
                homePossessionSumHardOpponent += new Match(home2, hardAway, random).Simulate().MatchStatsHome.Possession;
            }

            Assert.True(homePossessionSumSoftOpponent > homePossessionSumHardOpponent,
                $"soft={homePossessionSumSoftOpponent}, hard={homePossessionSumHardOpponent}");
        }
    }
}
