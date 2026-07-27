using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class GoalkeeperDuelTests
    {
        [Fact]
        public void Simulate_StrongGoalkeeper_ConcedesFewerGoalsThanWeakGoalkeeper()
        {
            var random = new Random(42);
            int goalsAgainstStrongKeeper = 0;
            int goalsAgainstWeakKeeper = 0;
            const int matches = 60;

            for (int i = 0; i < matches; i++)
            {
                var attacker1 = TestHelpers.CreateTeam("Angreifer", baseRating: 70);
                var strongKeeperTeam = TestHelpers.CreateTeam("StarkerKeeper", baseRating: 70);
                var strongKeeper = strongKeeperTeam.Players.Single(p => p.Position == Position.Goalkeeper);
                strongKeeper.DefensivePower = 95;
                strongKeeper.DuelEfficiency = 95;
                strongKeeper.GkReflexes = 95;
                strongKeeper.GkOneOnOne = 95;
                strongKeeper.GkHandling = 95;
                var result1 = new Match(attacker1, strongKeeperTeam, random).Simulate();
                goalsAgainstStrongKeeper += result1.HomeGoals;

                var attacker2 = TestHelpers.CreateTeam("Angreifer", baseRating: 70);
                var weakKeeperTeam = TestHelpers.CreateTeam("SchwacherKeeper", baseRating: 70);
                var weakKeeper = weakKeeperTeam.Players.Single(p => p.Position == Position.Goalkeeper);
                weakKeeper.DefensivePower = 20;
                weakKeeper.DuelEfficiency = 20;
                weakKeeper.GkReflexes = 20;
                weakKeeper.GkOneOnOne = 20;
                weakKeeper.GkHandling = 20;
                var result2 = new Match(attacker2, weakKeeperTeam, random).Simulate();
                goalsAgainstWeakKeeper += result2.HomeGoals;
            }

            Assert.True(goalsAgainstWeakKeeper > goalsAgainstStrongKeeper);
        }

        [Fact]
        public void Simulate_GreatFinisher_StillScoresSometimesAgainstGreatGoalkeeper()
        {
            var random = new Random(77);
            int goals = 0;
            const int matches = 60;

            for (int i = 0; i < matches; i++)
            {
                var attackingTeam = TestHelpers.CreateTeam("Angreifer", baseRating: 60);
                foreach (var p in attackingTeam.Players.Where(p => p.Position == Position.Forward))
                    p.OffensivePower = 95;

                var defendingTeam = TestHelpers.CreateTeam("Verteidiger", baseRating: 60);
                var keeper = defendingTeam.Players.Single(p => p.Position == Position.Goalkeeper);
                keeper.DefensivePower = 95;
                keeper.DuelEfficiency = 95;
                keeper.GkReflexes = 95;
                keeper.GkOneOnOne = 95;

                var result = new Match(attackingTeam, defendingTeam, random).Simulate();
                goals += result.HomeGoals;
            }

            Assert.True(goals > 0, "Ein sehr guter Torschütze soll auch gegen einen Top-Torwart gelegentlich treffen.");
        }

        [Fact]
        public void Simulate_GkSpecificAttributesAlone_StillSeparateStrongFromWeakGoalkeeper()
        {
            // DefensivePower/DuelEfficiency stay identical for both keepers here - only the new
            // GK-specific attributes (Reflexes/OneOnOne/Handling) differ, proving they alone
            // meaningfully influence the shot-vs-keeper duel.
            var random = new Random(11);
            int goalsAgainstStrongKeeper = 0;
            int goalsAgainstWeakKeeper = 0;
            const int matches = 200;

            for (int i = 0; i < matches; i++)
            {
                var attacker1 = TestHelpers.CreateTeam("Angreifer", baseRating: 70);
                var strongKeeperTeam = TestHelpers.CreateTeam("StarkerKeeper", baseRating: 70);
                var strongKeeper = strongKeeperTeam.Players.Single(p => p.Position == Position.Goalkeeper);
                strongKeeper.GkReflexes = 95;
                strongKeeper.GkOneOnOne = 95;
                var result1 = new Match(attacker1, strongKeeperTeam, random).Simulate();
                goalsAgainstStrongKeeper += result1.HomeGoals;

                var attacker2 = TestHelpers.CreateTeam("Angreifer", baseRating: 70);
                var weakKeeperTeam = TestHelpers.CreateTeam("SchwacherKeeper", baseRating: 70);
                var weakKeeper = weakKeeperTeam.Players.Single(p => p.Position == Position.Goalkeeper);
                weakKeeper.GkReflexes = 20;
                weakKeeper.GkOneOnOne = 20;
                var result2 = new Match(attacker2, weakKeeperTeam, random).Simulate();
                goalsAgainstWeakKeeper += result2.HomeGoals;
            }

            Assert.True(goalsAgainstWeakKeeper > goalsAgainstStrongKeeper);
        }

        [Fact]
        public void GetGoalkeeper_ReturnsThePlayerAtGoalkeeperPosition()
        {
            var team = TestHelpers.CreateTeam("Team", baseRating: 65);

            var goalkeeper = TeamStrengthCalculator.GetGoalkeeper(team);

            Assert.NotNull(goalkeeper);
            Assert.Equal(Position.Goalkeeper, goalkeeper!.Position);
        }
    }
}
