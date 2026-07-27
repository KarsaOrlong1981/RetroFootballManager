using SQLite;

namespace RetroFootballManager.Models
{
    // Ein gebuchtes Trainingslager - Effekt (Moral + ggf. Attribut-Boost) wird erst am EndDate
    // angewendet (siehe TrainingCampService.ApplyDueCampsAsync), nicht sofort bei Buchung.
    public class TrainingCamp
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int TeamId { get; set; }

        public TrainingCampTier Tier { get; set; }
        public int DurationWeeks { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double Cost { get; set; }
        public int MoraleBoost { get; set; }
        public bool GrantsAttributeBoost { get; set; }
        public bool Applied { get; set; }
    }
}
