using SQLite;

namespace RetroFootballManager.Models
{
    // Catalog/reference entry: a sponsor that can be offered to teams, not an active deal.
    public class Sponsor
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public SponsorType SponsorType { get; set; }
        public int MinTier { get; set; }              // weakest league tier (highest number) that still qualifies

        public int SeasonPayment { get; set; }
        public int BonusPerWin { get; set; }
        public int BonusPerPromotion { get; set; }
    }
}
