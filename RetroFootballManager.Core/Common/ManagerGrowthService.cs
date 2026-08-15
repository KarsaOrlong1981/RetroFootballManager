using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Mid-season manager skill growth: usage counters (matches coached, orientation-heavy
    // matches, training-focus weeks, applied talks) each grant +1 to their matching skill
    // once they reach GrowthThreshold, then reset to 0. The cap is the NEXT license tier's
    // ceiling, not the current one - lets a skill keep growing past today's license ceiling
    // over a long career, without ever reaching a state all 5 are maxed at once (todays
    // license's own budget rule, see ManagerProfileGenerator, still governs at generation).
    public static class ManagerGrowthService
    {
        public const int GrowthThreshold = 20;

        // A match "counts" toward Offensive Creation/Defensive Organization growth once a
        // side spent at least this many minutes under that orientation.
        public const int OrientationMinuteThreshold = 65;

        private static int NextTierCeiling(CoachingLicense license) => license switch
        {
            CoachingLicense.C => 5,    // B's ceiling
            CoachingLicense.B => 7,    // A's ceiling
            CoachingLicense.A => 10,   // Pro's ceiling
            CoachingLicense.Pro => 10, // already at the top
            _ => 10,
        };

        // Called once per finished match (league/cup/friendly - see MatchDayService.
        // ApplyCareerMinutes, the single shared hook all three funnel through) for each
        // side's manager.
        public static void ApplyMatchGrowth(ManagerProfile? manager, MatchResult result, bool isHome)
        {
            if (manager is null)
                return;

            manager.MatchesCoachedAccumulated++;
            if (manager.MatchesCoachedAccumulated >= GrowthThreshold)
            {
                manager.InGameCoaching = Math.Min(manager.InGameCoaching + 1, NextTierCeiling(manager.License));
                manager.MatchesCoachedAccumulated = 0;
            }

            int offensiveMinutes = isHome ? result.HomeOffensiveOrientationMinutes : result.AwayOffensiveOrientationMinutes;
            if (offensiveMinutes >= OrientationMinuteThreshold)
            {
                manager.OffensiveOrientationQualifyingMatches++;
                if (manager.OffensiveOrientationQualifyingMatches >= GrowthThreshold)
                {
                    manager.OffensiveCreation = Math.Min(manager.OffensiveCreation + 1, NextTierCeiling(manager.License));
                    manager.OffensiveOrientationQualifyingMatches = 0;
                }
            }

            int defensiveMinutes = isHome ? result.HomeDefensiveOrientationMinutes : result.AwayDefensiveOrientationMinutes;
            if (defensiveMinutes >= OrientationMinuteThreshold)
            {
                manager.DefensiveOrientationQualifyingMatches++;
                if (manager.DefensiveOrientationQualifyingMatches >= GrowthThreshold)
                {
                    manager.DefensiveOrganization = Math.Min(manager.DefensiveOrganization + 1, NextTierCeiling(manager.License));
                    manager.DefensiveOrientationQualifyingMatches = 0;
                }
            }
        }

        // Weekly hook, called alongside TrainingService.ApplyWeeklyTraining - +1 per week
        // with an active TeamTrainingFocus.
        public static void ApplyWeeklyTrainingFocusGrowth(ManagerProfile? manager, Team team)
        {
            if (manager is null || team.TeamTrainingFocus is null)
                return;

            manager.TrainingFocusWeeksAccumulated++;
            if (manager.TrainingFocusWeeksAccumulated >= GrowthThreshold)
            {
                manager.TrainingDesign = Math.Min(manager.TrainingDesign + 1, NextTierCeiling(manager.License));
                manager.TrainingFocusWeeksAccumulated = 0;
            }
        }

        // Called from ConversationService.Talk / TeamTalkService.TryApply themselves once a
        // talk actually applies - no separate anti-abuse mechanism needed, both already have
        // their own cooldown/overpraise logic.
        public static void ApplyTalkGrowth(ManagerProfile? manager)
        {
            if (manager is null)
                return;

            manager.MotivationalTalksAccumulated++;
            if (manager.MotivationalTalksAccumulated >= GrowthThreshold)
            {
                manager.Motivation = Math.Min(manager.Motivation + 1, NextTierCeiling(manager.License));
                manager.MotivationalTalksAccumulated = 0;
            }
        }
    }
}
