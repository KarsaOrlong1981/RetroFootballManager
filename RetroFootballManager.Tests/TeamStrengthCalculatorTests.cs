using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class TeamStrengthCalculatorTests
    {
        [Fact]
        public void HigherRatedTeam_HasHigherOverallStrength()
        {
            var strong = TestHelpers.CreateTeam("Stark", baseRating: 80);
            var weak = TestHelpers.CreateTeam("Schwach", baseRating: 40);

            var strongProfile = TeamStrengthCalculator.Calculate(strong, isHome: false);
            var weakProfile = TeamStrengthCalculator.Calculate(weak, isHome: false);

            Assert.True(strongProfile.Overall > weakProfile.Overall);
        }

        [Fact]
        public void HigherMorale_IncreasesStrength()
        {
            var lowMorale = TestHelpers.CreateTeam("LowMorale", baseRating: 60, morale: 20);
            var highMorale = TestHelpers.CreateTeam("HighMorale", baseRating: 60, morale: 90);

            var lowProfile = TeamStrengthCalculator.Calculate(lowMorale, isHome: false);
            var highProfile = TeamStrengthCalculator.Calculate(highMorale, isHome: false);

            Assert.True(highProfile.Overall > lowProfile.Overall);
        }

        [Fact]
        public void LowFitness_ReducesStrength()
        {
            var tired = TestHelpers.CreateTeam("Müde", baseRating: 60, fitness: 40);
            var fresh = TestHelpers.CreateTeam("Frisch", baseRating: 60, fitness: 95);

            var tiredProfile = TeamStrengthCalculator.Calculate(tired, isHome: false);
            var freshProfile = TeamStrengthCalculator.Calculate(fresh, isHome: false);

            Assert.True(freshProfile.Overall > tiredProfile.Overall);
        }

        [Fact]
        public void HomeStadiumAdvantage_BoostsHomeTeamOnly()
        {
            var stadium = new Stadium { Name = "Retro Arena", HomeAdvantage = 90, Atmosphere = 90, Condition = 90 };
            var team = TestHelpers.CreateTeam("Heim", baseRating: 60, stadium: stadium);

            var homeProfile = TeamStrengthCalculator.Calculate(team, isHome: true);
            var awayProfile = TeamStrengthCalculator.Calculate(team, isHome: false);

            Assert.True(homeProfile.Overall > awayProfile.Overall);
        }

        [Fact]
        public void HigherDribbling_IncreasesAttack()
        {
            var lowDribbling = TestHelpers.CreateTeam("SchwachDribbling", baseRating: 60);
            foreach (var p in lowDribbling.Players.Where(p => p.Position != Position.Goalkeeper))
                p.Dribbling = 10;

            var highDribbling = TestHelpers.CreateTeam("StarkDribbling", baseRating: 60);
            foreach (var p in highDribbling.Players.Where(p => p.Position != Position.Goalkeeper))
                p.Dribbling = 95;

            var lowProfile = TeamStrengthCalculator.Calculate(lowDribbling, isHome: false);
            var highProfile = TeamStrengthCalculator.Calculate(highDribbling, isHome: false);

            Assert.True(highProfile.Attack > lowProfile.Attack);
        }

        [Fact]
        public void OffensiveOrientation_IncreasesAttack_ButLowersDefense()
        {
            var team = TestHelpers.CreateTeam("Offensiv", baseRating: 60, orientation: TacticalOrientation.Offensive);
            var balanced = TestHelpers.CreateTeam("Balanced", baseRating: 60, orientation: TacticalOrientation.Balanced);

            var offensiveProfile = TeamStrengthCalculator.Calculate(team, isHome: false);
            var balancedProfile = TeamStrengthCalculator.Calculate(balanced, isHome: false);

            Assert.True(offensiveProfile.Attack > balancedProfile.Attack);
            Assert.True(offensiveProfile.Defense < balancedProfile.Defense);
        }
    }
}
