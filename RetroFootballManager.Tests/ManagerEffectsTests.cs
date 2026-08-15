using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class ManagerEffectsTests
    {
        private static ManagerProfile Profile(CoachingLicense license, int skill) => new()
        {
            License = license,
            TrainingDesign = skill,
            Motivation = skill,
            OffensiveCreation = skill,
            DefensiveOrganization = skill,
            InGameCoaching = skill,
        };

        [Fact]
        public void Factor_NullProfile_IsNeutral()
        {
            Assert.Equal(1.0, ManagerEffects.TrainingDesignFactor(null));
            Assert.Equal(1.0, ManagerEffects.MotivationFactor(null));
            Assert.Equal(1.0, ManagerEffects.OffensiveCreationFactor(null));
            Assert.Equal(1.0, ManagerEffects.DefensiveOrganizationFactor(null));
            Assert.Equal(1.0, ManagerEffects.InGameCoachingFactor(null));
        }

        [Theory]
        [InlineData(CoachingLicense.C, CoachingLicense.B)]
        [InlineData(CoachingLicense.B, CoachingLicense.A)]
        [InlineData(CoachingLicense.A, CoachingLicense.Pro)]
        public void Factor_HigherLicense_SameSkill_IsStronger(CoachingLicense lower, CoachingLicense higher)
        {
            var lowerProfile = Profile(lower, 7);
            var higherProfile = Profile(higher, 7);

            Assert.True(
                ManagerEffects.TrainingDesignFactor(higherProfile) > ManagerEffects.TrainingDesignFactor(lowerProfile));
        }

        [Fact]
        public void Factor_HigherSkill_SameLicense_IsStronger()
        {
            var weak = Profile(CoachingLicense.A, 2);
            var strong = Profile(CoachingLicense.A, 9);

            Assert.True(ManagerEffects.MotivationFactor(strong) > ManagerEffects.MotivationFactor(weak));
        }

        [Fact]
        public void Factor_ProLicenseSkill7_BeatsCLicenseSkill7()
        {
            // The exact scenario called out in the plan: a "7" with a Pro license should
            // clearly outperform a "7" with a C license, not just marginally.
            var cLicense = Profile(CoachingLicense.C, 7);
            var proLicense = Profile(CoachingLicense.Pro, 7);

            double cFactor = ManagerEffects.InGameCoachingFactor(cLicense);
            double proFactor = ManagerEffects.InGameCoachingFactor(proLicense);

            Assert.True(proFactor > cFactor * 1.2, $"cFactor={cFactor}, proFactor={proFactor}");
        }

        [Fact]
        public void AnnualSalary_HigherLicenseAndSkills_PaysMore()
        {
            var junior = Profile(CoachingLicense.C, 2);
            var senior = Profile(CoachingLicense.Pro, 9);

            Assert.True(ManagerEffects.AnnualSalary(senior) > ManagerEffects.AnnualSalary(junior));
        }

        [Fact]
        public void ConversationServiceTalk_WithStrongerManager_ProducesBiggerMoraleSwing()
        {
            var state = new GameState { CurrentDate = new DateTime(2026, 8, 1) };
            var weakManager = Profile(CoachingLicense.C, 1);
            var strongManager = Profile(CoachingLicense.Pro, 10);

            var playerA = TestHelpers.CreateTeam("A", baseRating: 60).Players[0];
            playerA.Moral = 50;
            var resultWeak = ConversationService.Talk(playerA, TalkType.Praise, state, matchFactor: 2, manager: weakManager);

            var playerB = TestHelpers.CreateTeam("B", baseRating: 60).Players[0];
            playerB.Moral = 50;
            var resultStrong = ConversationService.Talk(playerB, TalkType.Praise, state, matchFactor: 2, manager: strongManager);

            Assert.True(Math.Abs(resultStrong.MoralDelta) > Math.Abs(resultWeak.MoralDelta));
        }

        [Fact]
        public void TeamStrengthCalculator_OffensiveCreation_OnlyAffectsAttackNotMidfieldOrPressing()
        {
            var weakTeam = TestHelpers.CreateTeam("Weak", baseRating: 60);
            weakTeam.ManagerProfile = Profile(CoachingLicense.C, 1);
            var strongTeam = TestHelpers.CreateTeam("Strong", baseRating: 60);
            strongTeam.ManagerProfile = Profile(CoachingLicense.Pro, 10);

            var weakProfile = TeamStrengthCalculator.Calculate(weakTeam, isHome: false);
            var strongProfile = TeamStrengthCalculator.Calculate(strongTeam, isHome: false);

            Assert.True(strongProfile.Attack > weakProfile.Attack);
            Assert.True(strongProfile.Defense > weakProfile.Defense);
            // Overall/Midfield/Pressing use identical squads/tactics and no manager factor -
            // must stay unaffected by the manager profile.
            Assert.Equal(weakProfile.Midfield, strongProfile.Midfield, 6);
            Assert.Equal(weakProfile.Pressing, strongProfile.Pressing, 6);
            Assert.Equal(weakProfile.Overall, strongProfile.Overall, 6);
        }
    }
}
