using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public enum StadiumUpgradeKind { Comfort, Catering, Merchandise, Infrastructure }

    public record UpgradeCost(double Amount, string Description);

    // Stadium upgrade costs and applying them to a team - pure calculation, no DB access;
    // the caller saves itself after a successful TryApplyUpgrade.
    public static class StadiumService
    {
        private const double SeatUnitCost = 200;
        private const double StandingUnitCost = 80;
        private const double LogeUnitCost = 900;
        private const double RoofCostPerSeat = 150;
        private const double LevelUpgradeBaseCost = 50_000;
        private const int MaxLevel = 5;

        // A bigger/better stadium also costs more upkeep - every capacity or level
        // upgrade permanently increases MaintenanceCosts too.
        private const double SeatMaintenancePerUnit = 0.5;
        private const double StandingMaintenancePerUnit = 0.3;
        private const double LogeMaintenancePerUnit = 2.0;
        private const double RoofMaintenanceIncrease = 5_000;
        private const double LevelUpgradeMaintenanceIncrease = 3_000;

        // Capacity "evolution stages" (0-6), also used by the stadium background image
        // (see StadiumViewModel.EvolutionImages) - a shared source for both.
        private static readonly int[] EvolutionStageUpperBounds = [10_000, 15_000, 25_000, 45_000, 60_000, 80_000];

        public static int GetEvolutionStage(int capacity)
        {
            for (int i = 0; i < EvolutionStageUpperBounds.Length; i++)
                if (capacity < EvolutionStageUpperBounds[i])
                    return i;
            return EvolutionStageUpperBounds.Length; // 6 = ultimate (>= 80_000)
        }

        // The higher the current evolution stage, the more expensive further capacity gets -
        // stops a team (e.g. via a single loan) from upgrading the stadium from "minimal" to
        // "ultimate" in one go.
        private static readonly double[] EvolutionStageCostMultiplier = [1.0, 1.5, 2.2, 3.2, 4.5, 6.0, 8.0];

        private static double StageMultiplier(Stadium stadium) =>
            EvolutionStageCostMultiplier[GetEvolutionStage(stadium.Capacity)];

        private static double InfrastructureDiscount(Stadium stadium) =>
            1 + 0.1 * (stadium.InfrastructureLevel - 1);

        public static UpgradeCost GetSeatingUpgradeCost(Stadium stadium, int addSeats) =>
            new(addSeats * SeatUnitCost * StageMultiplier(stadium) / InfrastructureDiscount(stadium), $"+{addSeats} Sitzplätze");

        public static UpgradeCost GetStandingUpgradeCost(Stadium stadium, int addStanding) =>
            new(addStanding * StandingUnitCost * StageMultiplier(stadium) / InfrastructureDiscount(stadium), $"+{addStanding} Stehplätze");

        public static UpgradeCost GetLogeUpgradeCost(Stadium stadium, int addLoge) =>
            new(addLoge * LogeUnitCost * StageMultiplier(stadium) / InfrastructureDiscount(stadium), $"+{addLoge} Logenplätze");

        public static UpgradeCost GetRoofCost(Stadium stadium) =>
            new(stadium.SeatingCapacity * RoofCostPerSeat, "Überdachung");

        public static UpgradeCost GetLevelUpgradeCost(Stadium stadium, StadiumUpgradeKind kind)
        {
            int currentLevel = kind switch
            {
                StadiumUpgradeKind.Comfort => stadium.ComfortLevel,
                StadiumUpgradeKind.Catering => stadium.CateringLevel,
                StadiumUpgradeKind.Merchandise => stadium.MerchandiseLevel,
                StadiumUpgradeKind.Infrastructure => stadium.InfrastructureLevel,
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };

            int nextLevel = Math.Min(currentLevel + 1, MaxLevel);
            double cost = nextLevel * nextLevel * LevelUpgradeBaseCost;
            return new UpgradeCost(cost, $"{kind} auf Stufe {nextLevel}");
        }

        // Capacity/level upgrades that also raise ongoing upkeep (MaintenanceCosts) in
        // addition to the actual change - so a bigger/better stadium costs more permanently,
        // not just once at upgrade time.
        public static void ApplySeatingUpgrade(Stadium stadium, int addSeats)
        {
            stadium.SeatingCapacity += addSeats;
            stadium.MaintenanceCosts += addSeats * SeatMaintenancePerUnit;
        }

        public static void ApplyStandingUpgrade(Stadium stadium, int addStanding)
        {
            stadium.StandingCapacity += addStanding;
            stadium.MaintenanceCosts += addStanding * StandingMaintenancePerUnit;
        }

        public static void ApplyLogeUpgrade(Stadium stadium, int addLoge)
        {
            stadium.LogeCapacity += addLoge;
            stadium.MaintenanceCosts += addLoge * LogeMaintenancePerUnit;
        }

        public static void ApplyRoof(Stadium stadium)
        {
            stadium.HasRoof = true;
            stadium.MaintenanceCosts += RoofMaintenanceIncrease;
        }

        public static void ApplyLevelUpgrade(Stadium stadium, StadiumUpgradeKind kind)
        {
            switch (kind)
            {
                case StadiumUpgradeKind.Comfort: stadium.ComfortLevel = Math.Min(stadium.ComfortLevel + 1, MaxLevel); break;
                case StadiumUpgradeKind.Catering: stadium.CateringLevel = Math.Min(stadium.CateringLevel + 1, MaxLevel); break;
                case StadiumUpgradeKind.Merchandise: stadium.MerchandiseLevel = Math.Min(stadium.MerchandiseLevel + 1, MaxLevel); break;
                case StadiumUpgradeKind.Infrastructure: stadium.InfrastructureLevel = Math.Min(stadium.InfrastructureLevel + 1, MaxLevel); break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }

            stadium.MaintenanceCosts += LevelUpgradeMaintenanceIncrease;
        }

        // Applies the upgrade only if enough money is available; deducts the cost from the
        // balance. Returns false (no change) if the balance is insufficient.
        public static bool TryApplyUpgrade(Team team, Action<Stadium> upgrade, double cost)
        {
            if (team.Stadium is null || team.Finances is null)
                return false;

            if (team.Finances.CurrentBalance < cost)
                return false;

            upgrade(team.Stadium);
            team.Finances.CurrentBalance -= (int)cost;
            return true;
        }
    }
}
