using SQLite;

namespace RetroFootballManager.Models
{
    // Singleton row (Id = 1) describing the current game state,
    // so the game can always be resumed.
    public class GameState
    {
        [PrimaryKey]
        public int Id { get; set; } = 1;

        public string SaveName { get; set; } = string.Empty;
        public int ManagerTeamId { get; set; }
        public int Season { get; set; } = 1;
        public DateTime CurrentDate { get; set; }

        // Fixed anchor for the cup/tournament calendar (CupCalendarService) - unlike
        // CurrentDate, this value doesn't change within a season.
        public DateTime SeasonStart { get; set; }

        public int MatchdayIndex { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastSavedAt { get; set; }

        // Controls how fast COM teams train/develop (see TrainingService).
        public Difficulty Difficulty { get; set; } = Difficulty.Normal;
    }
}
