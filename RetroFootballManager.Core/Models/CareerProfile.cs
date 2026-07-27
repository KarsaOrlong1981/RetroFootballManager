namespace RetroFootballManager.Models
{
    // Global meta-progression of the player - deliberately NOT part of a save game,
    // stored separately (as JSON) instead. Once a league is unlocked it stays unlocked
    // across all save games. Managed by CareerService.
    public class CareerProfile
    {
        // Total points from achievements (promotions, championships, standings, ...).
        public int Points { get; set; }

        // Optional log of point awards (for an achievements/history view).
        public List<CareerAward> Awards { get; set; } = [];
    }

    // A single point award (e.g. "promoted to league 2, season 3, +100").
    public class CareerAward
    {
        public int Season { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int Points { get; set; }
    }
}
