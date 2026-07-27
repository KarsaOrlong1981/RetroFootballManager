using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Player contracts: seeding at career start and simple renewal. Real negotiation
    // (counter-offer/rejection) is part of the later M5 transfer system.
    public static class PlayerContractService
    {
        // team.Players only contains senior players (youth players are in Team.YouthPlayers) -
        // all get a 2-year contract at career start.
        public static List<Contract> SeedInitialContracts(Team team, DateTime seasonStart) =>
            team.Players.Select(p => new Contract
            {
                HolderId = p.Id,
                HolderType = ContractHolderType.Player,
                TeamId = team.Id,
                StartDate = seasonStart,
                EndDate = seasonStart.AddYears(2),
                AnnualSalary = PlayerValuationService.EstimateAnnualSalary(p),
                MarketValue = PlayerValuationService.EstimateMarketValue(p),
            }).ToList();

        public static Contract? GetActiveContract(int playerId, IEnumerable<Contract> contracts, DateTime asOf) =>
            contracts
                .Where(c => c.HolderId == playerId && c.HolderType == ContractHolderType.Player && c.EndDate > asOf)
                .OrderByDescending(c => c.EndDate)
                .FirstOrDefault();

        public static void RenewContract(Contract contract, double newAnnualSalary, int additionalYears)
        {
            contract.AnnualSalary = newAnnualSalary;
            contract.EndDate = contract.EndDate.AddYears(additionalYears);
        }
    }
}
