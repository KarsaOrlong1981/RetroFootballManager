using SQLite;

namespace RetroFootballManager.Models
{
    // A league at a given tier (1-4) in a season. 4 leagues of 18 teams each.
    public class League
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        // 1 = top league ... 4 = bottom (player's starting league).
        public int Tier { get; set; }

        public int Season { get; set; }
    }
}
