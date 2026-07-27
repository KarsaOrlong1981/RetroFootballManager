using SQLite;

namespace RetroFootballManager.Models
{
    // An active deal linking a team to a sponsor for one of its three slots.
    public class Sponsorship
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int TeamId { get; set; }
        public int SponsorId { get; set; }
        public SponsorType SponsorType { get; set; }   // denormalized for quick per-slot lookup

        public int StartSeason { get; set; }
        public int Duration { get; set; }              // seasons
    }
}
