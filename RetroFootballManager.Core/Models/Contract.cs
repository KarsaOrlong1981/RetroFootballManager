using SQLite;

namespace RetroFootballManager.Models
{
    public class Contract
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int HolderId { get; set; }         // PlayerId or EmployeeId, depending on HolderType
        public ContractHolderType HolderType { get; set; }

        [Indexed]
        public int TeamId { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public double AnnualSalary { get; set; }
        public double MarketValue { get; set; }
        public double SigningBonus { get; set; }
        public double ReleaseClause { get; set; }
    }
}
