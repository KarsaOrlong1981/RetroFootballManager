using SQLite;

namespace RetroFootballManager.Models
{
    // An active scouting assignment: TeamId is always the scouting (human) team.
    // Once CompletionDate is reached, player.IsScouted is set and the row is deleted - no
    // history needed, see CalendarAdvanceService/ScoutingService.ApplyDueScoutingAsync.
    public class ScoutingAssignment
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int TeamId { get; set; }

        public int PlayerId { get; set; }

        // Which Scout Employee is working this assignment - each scout can carry at most
        // MaxConcurrentAssignmentsPerScout (see ScoutingService) at once. 0 for assignments
        // persisted before this field existed (legacy rows - excluded from capacity counting).
        public int ScoutEmployeeId { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime CompletionDate { get; set; }
    }
}
