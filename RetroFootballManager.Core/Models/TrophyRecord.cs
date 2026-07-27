using SQLite;

namespace RetroFootballManager.Models
{
    // How often (and when last) the team won a title - one row per (TeamId, Type).
    // Only maintained for the manager's team (see SaveGameService.RecordTrophyWinAsync),
    // not for all 72 teams.
    public class TrophyRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int TeamId { get; set; }

        public TrophyType Type { get; set; }
        public int Count { get; set; }
        public int LastWonSeason { get; set; }
    }
}
