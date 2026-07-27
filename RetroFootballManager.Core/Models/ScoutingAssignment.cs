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
        public DateTime StartDate { get; set; }
        public DateTime CompletionDate { get; set; }
    }
}
