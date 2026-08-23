using SQLite;

namespace RetroFootballManager.Models
{
    // A single performance-bonus line agreed as part of a Contract (see ContractBonusType).
    public class ContractBonus
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int ContractId { get; set; }

        public ContractBonusType BonusType { get; set; }
        public double Amount { get; set; }
    }
}
