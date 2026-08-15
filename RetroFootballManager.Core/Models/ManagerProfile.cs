using SQLite;

namespace RetroFootballManager.Models
{
    // Head coach/manager profile - one per team (human or AI), mirrors the Stadium/Finances
    // 1:1-per-team persistence pattern (see TeamRepository). Skills are 1-10, budgeted by
    // license tier (see ManagerProfileGenerator) so no team can ever field a manager maxed
    // out on all five at once.
    public class ManagerProfile
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int TeamId { get; set; }

        public bool IsHuman { get; set; }

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }

        // null = fall back to the default placeholder (Resources/Images/avatar.png).
        public string? AvatarPath { get; set; }

        public CoachingLicense License { get; set; }

        public int TrainingDesign { get; set; }
        public int Motivation { get; set; }
        public int OffensiveCreation { get; set; }
        public int DefensiveOrganization { get; set; }
        public int InGameCoaching { get; set; }

        // Points left to distribute in the "Punkte verteilen" flow - only ever non-zero right
        // after human creation if the budget wasn't fully spent; AI profiles are always 0.
        public int UnspentSkillPoints { get; set; }

        // Growth counters - see ManagerGrowthService. Each reaching its threshold (20) grants
        // +1 to the matching skill (capped at the next license tier's ceiling) and resets to 0.
        public int OffensiveOrientationQualifyingMatches { get; set; }
        public int DefensiveOrientationQualifyingMatches { get; set; }
        public int TrainingFocusWeeksAccumulated { get; set; }
        public int MotivationalTalksAccumulated { get; set; }
        public int MatchesCoachedAccumulated { get; set; }
    }
}
