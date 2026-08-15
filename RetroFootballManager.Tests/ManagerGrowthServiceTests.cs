using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class ManagerGrowthServiceTests
    {
        private static ManagerProfile FreshProfile(CoachingLicense license = CoachingLicense.B, int skill = 3) => new()
        {
            License = license,
            TrainingDesign = skill,
            Motivation = skill,
            OffensiveCreation = skill,
            DefensiveOrganization = skill,
            InGameCoaching = skill,
        };

        [Fact]
        public void ApplyMatchGrowth_20Matches_GrowsInGameCoachingAndResetsCounter()
        {
            var manager = FreshProfile();
            var result = new MatchResult();

            for (int i = 0; i < ManagerGrowthService.GrowthThreshold - 1; i++)
                ManagerGrowthService.ApplyMatchGrowth(manager, result, isHome: true);

            Assert.Equal(3, manager.InGameCoaching);
            Assert.Equal(ManagerGrowthService.GrowthThreshold - 1, manager.MatchesCoachedAccumulated);

            ManagerGrowthService.ApplyMatchGrowth(manager, result, isHome: true);

            Assert.Equal(4, manager.InGameCoaching);
            Assert.Equal(0, manager.MatchesCoachedAccumulated);
        }

        [Fact]
        public void ApplyMatchGrowth_InGameCoaching_CappedAtNextTierCeiling()
        {
            // License B -> next tier ceiling is A's (7).
            var manager = FreshProfile(CoachingLicense.B, skill: 7);
            var result = new MatchResult();

            for (int i = 0; i < ManagerGrowthService.GrowthThreshold * 3; i++)
                ManagerGrowthService.ApplyMatchGrowth(manager, result, isHome: true);

            Assert.Equal(7, manager.InGameCoaching);
        }

        [Fact]
        public void ApplyMatchGrowth_OffensiveOrientation_OnlyCountsWhenMinutesReachThreshold()
        {
            var manager = FreshProfile();
            var belowThreshold = new MatchResult { HomeOffensiveOrientationMinutes = ManagerGrowthService.OrientationMinuteThreshold - 1 };
            var atThreshold = new MatchResult { HomeOffensiveOrientationMinutes = ManagerGrowthService.OrientationMinuteThreshold };

            ManagerGrowthService.ApplyMatchGrowth(manager, belowThreshold, isHome: true);
            Assert.Equal(0, manager.OffensiveOrientationQualifyingMatches);

            ManagerGrowthService.ApplyMatchGrowth(manager, atThreshold, isHome: true);
            Assert.Equal(1, manager.OffensiveOrientationQualifyingMatches);
        }

        [Fact]
        public void ApplyMatchGrowth_20QualifyingOffensiveMatches_GrowsOffensiveCreation()
        {
            var manager = FreshProfile();
            var qualifying = new MatchResult { AwayOffensiveOrientationMinutes = ManagerGrowthService.OrientationMinuteThreshold };

            for (int i = 0; i < ManagerGrowthService.GrowthThreshold; i++)
                ManagerGrowthService.ApplyMatchGrowth(manager, qualifying, isHome: false);

            Assert.Equal(4, manager.OffensiveCreation);
            Assert.Equal(0, manager.OffensiveOrientationQualifyingMatches);
        }

        [Fact]
        public void ApplyMatchGrowth_20QualifyingDefensiveMatches_GrowsDefensiveOrganization()
        {
            var manager = FreshProfile();
            var qualifying = new MatchResult { HomeDefensiveOrientationMinutes = ManagerGrowthService.OrientationMinuteThreshold };

            for (int i = 0; i < ManagerGrowthService.GrowthThreshold; i++)
                ManagerGrowthService.ApplyMatchGrowth(manager, qualifying, isHome: true);

            Assert.Equal(4, manager.DefensiveOrganization);
        }

        [Fact]
        public void ApplyMatchGrowth_NullManager_DoesNotThrow()
        {
            var result = new MatchResult();
            ManagerGrowthService.ApplyMatchGrowth(null, result, isHome: true);
        }

        [Fact]
        public void ApplyWeeklyTrainingFocusGrowth_20WeeksWithFocus_GrowsTrainingDesign()
        {
            var manager = FreshProfile();
            var team = TestHelpers.CreateTeam("Fokus FC", baseRating: 60);
            team.TeamTrainingFocus = TeamTrainingFocus.Pressing;

            for (int i = 0; i < ManagerGrowthService.GrowthThreshold; i++)
                ManagerGrowthService.ApplyWeeklyTrainingFocusGrowth(manager, team);

            Assert.Equal(4, manager.TrainingDesign);
            Assert.Equal(0, manager.TrainingFocusWeeksAccumulated);
        }

        [Fact]
        public void ApplyWeeklyTrainingFocusGrowth_NoFocusSet_NeverAccumulates()
        {
            var manager = FreshProfile();
            var team = TestHelpers.CreateTeam("Ohne Fokus FC", baseRating: 60);
            team.TeamTrainingFocus = null;

            for (int i = 0; i < ManagerGrowthService.GrowthThreshold; i++)
                ManagerGrowthService.ApplyWeeklyTrainingFocusGrowth(manager, team);

            Assert.Equal(0, manager.TrainingFocusWeeksAccumulated);
            Assert.Equal(3, manager.TrainingDesign);
        }

        [Fact]
        public void ApplyTalkGrowth_20Talks_GrowsMotivation()
        {
            var manager = FreshProfile();

            for (int i = 0; i < ManagerGrowthService.GrowthThreshold; i++)
                ManagerGrowthService.ApplyTalkGrowth(manager);

            Assert.Equal(4, manager.Motivation);
            Assert.Equal(0, manager.MotivationalTalksAccumulated);
        }

        [Fact]
        public void ConversationServiceTalk_AppliedTalks_GrowManagerMotivation()
        {
            var manager = FreshProfile();
            var team = TestHelpers.CreateTeam("Gespräch FC", baseRating: 60);
            var player = team.Players[0];
            player.Moral = 50;
            var date = new DateTime(2026, 1, 1);

            for (int i = 0; i < ManagerGrowthService.GrowthThreshold; i++)
            {
                var result = ConversationService.Talk(player, TalkType.Praise, new GameState { CurrentDate = date }, manager: manager);
                Assert.True(result.Applied);
                date = date.AddDays(8); // past the 7-day cooldown
            }

            Assert.Equal(4, manager.Motivation);
        }

        [Fact]
        public void TeamTalkServiceTryApply_20Talks_GrowManagerMotivation()
        {
            var manager = FreshProfile();
            var home = TestHelpers.CreateTeam("Heim FC", baseRating: 60);
            home.ManagerProfile = manager;
            var away = TestHelpers.CreateTeam("Gast FC", baseRating: 60);
            var match = new Match(home, away);

            for (int i = 0; i < ManagerGrowthService.GrowthThreshold; i++)
                Assert.True(TeamTalkService.TryApply(match, isHome: true, TeamTalkOption.TacticalTalk));

            Assert.Equal(4, manager.Motivation);
        }

        [Fact]
        public void TeamTalkServiceTryApply_SayNothing_DoesNotGrowMotivation()
        {
            var manager = FreshProfile();
            var home = TestHelpers.CreateTeam("Heim FC", baseRating: 60);
            home.ManagerProfile = manager;
            var away = TestHelpers.CreateTeam("Gast FC", baseRating: 60);
            var match = new Match(home, away);

            for (int i = 0; i < ManagerGrowthService.GrowthThreshold; i++)
                TeamTalkService.TryApply(match, isHome: true, TeamTalkOption.SayNothing);

            Assert.Equal(3, manager.Motivation);
        }
    }
}
