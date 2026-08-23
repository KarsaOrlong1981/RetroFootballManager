using SQLite;

namespace RetroFootballManager.Models
{
    // Blocks a team from re-opening negotiations for a player for the rest of the season,
    // set when the counterpart's mood reaches NegotiationMoodLevel.Furious (transfer/loan
    // buy) or a contract renewal is declined (see NegotiationCooldownRepository).
    public class NegotiationCooldown
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int BuyingTeamId { get; set; }

        [Indexed]
        public int PlayerId { get; set; }

        public int Season { get; set; }
    }
}
