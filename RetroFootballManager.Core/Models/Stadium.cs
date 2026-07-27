using SQLite;

namespace RetroFootballManager.Models
{
    public class Stadium
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int TeamId { get; set; }

        public string Name { get; set; } = string.Empty;

        // Core attributes
        public int Condition { get; set; }              // 0–100: stadium quality
        public int Atmosphere { get; set; }             // 0–100: fan pressure & noise

        // Seating tiers
        public int SeatingCapacity { get; set; }
        public int StandingCapacity { get; set; }
        public int LogeCapacity { get; set; }

        [Ignore]
        public int Capacity => SeatingCapacity + StandingCapacity + LogeCapacity;

        // Financial impact
        public double MaintenanceCosts { get; set; }       // Monthly/seasonal costs
        public double TicketPrice { get; set; }            // Average ticket price (legacy/general)
        public double SeatPrice { get; set; }
        public double StandingPrice { get; set; }
        public double LogePrice { get; set; }

        // Facilities & upgrades (1–5)
        public bool HasRoof { get; set; }
        public int ComfortLevel { get; set; } = 1;
        public int CateringLevel { get; set; } = 1;
        public int MerchandiseLevel { get; set; } = 1;
        public int InfrastructureLevel { get; set; } = 1;

        // Matchday modifiers
        public int HomeAdvantage { get; set; }          // 0–100: boosts team performance
        public int WeatherResistance { get; set; }      // 0–100: bad weather impact
    }
}
