using SQLite;

namespace RetroFootballManager.Models
{
    public class Employee
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int TeamId { get; set; }

        public string Name { get; set; } = string.Empty;
        public double Rating { get; set; }
        public EmployeeType EmployeeType { get; set; }
        public Nationality Nationality { get; set; }

        // Age/Gender drive which NewGen portrait pool FaceImageAssigner picks from
        // (Age 0 = pre-existing save, backfilled once via StaffGenerator.BackfillAgeIfMissing).
        public int Age { get; set; }
        public Gender Gender { get; set; }
        public string? ImagePath { get; set; }

        // Specialised coaching ratings (1-99). A co-trainer strong in offensive training
        // boosts attackers' offensive drills; a weak goalkeeper trainer hurts keeper drills.
        public int OffensiveTraining { get; set; }
        public int DefensiveTraining { get; set; }
        public int GoalkeeperTraining { get; set; }
        public int FitnessTraining { get; set; }
        public int YouthDevelopment { get; set; }
        public int ScoutingAbility { get; set; }
        public int Motivation { get; set; }
        public int AnalysisAbility { get; set; }

        // Contract/market info (full negotiation comes with the staff market in M4).
        public double MarketValue { get; set; }
        public double Salary { get; set; }
    }
}
