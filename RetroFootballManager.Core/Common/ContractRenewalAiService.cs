using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // AI counterpart to the manual "renew" action (M6e): COM teams automatically renew expiring
    // player contracts when the player is still worth it (above average or young/promising) -
    // otherwise the team deliberately lets the contract expire.
    public static class ContractRenewalAiService
    {
        // Contracts are checked starting this many days before expiry.
        private const int RenewalWindowDays = 180;
        private const double RenewalRaise = 1.1;

        public static async Task RunWeeklyTickAsync(
            Team team, IReadOnlyList<Contract> teamPlayerContracts, ContractRepository contracts, DateTime currentDate)
        {
            double squadAverage = team.Players.Count > 0 ? team.Players.Average(p => p.Rating) : 0;

            var expiringSoon = teamPlayerContracts.Where(c =>
                c.HolderType == ContractHolderType.Player &&
                c.EndDate > currentDate &&
                (c.EndDate - currentDate).TotalDays <= RenewalWindowDays);

            foreach (var contract in expiringSoon)
            {
                var player = team.Players.FirstOrDefault(p => p.Id == contract.HolderId);
                if (player is null || !ShouldRenew(player, squadAverage))
                    continue;

                double newSalary = Math.Round(PlayerValuationService.EstimateAnnualSalary(player) * RenewalRaise);
                PlayerContractService.RenewContract(contract, newSalary, additionalYears: 2);
                await contracts.SaveAsync(contract);
            }
        }

        private static bool ShouldRenew(Player player, double squadAverage) =>
            player.Rating >= squadAverage - 5 || player.Age <= 26;
    }
}
