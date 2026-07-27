using SQLite;

namespace RetroFootballManager.Models
{
    // Persistent "already scouted" list for the scouting overview - independent of
    // Player.IsScouted (that remains a permanent, global flag). This row is purely for
    // visibility in the list and can be removed without affecting IsScouted.
    public class ScoutedPlayer
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int TeamId { get; set; }

        public int PlayerId { get; set; }
        public DateTime ScoutedDate { get; set; }
    }
}
