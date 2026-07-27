using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public class AiManagerService
    {
        private readonly TransferMarketService _transferMarket;
        private readonly StaffMarketService _staffMarket;
        private readonly ContractRepository _contracts;
        private readonly TransferListingRepository _listings;

        public AiManagerService(
            TransferMarketService transferMarket, StaffMarketService staffMarket,
            ContractRepository contracts, TransferListingRepository listings)
        {
            _transferMarket = transferMarket;
            _staffMarket = staffMarket;
            _contracts = contracts;
            _listings = listings;
        }

        public async Task RunWeeklyTickAsync(
            Team team, int season, DateTime currentDate, Difficulty difficulty, Random rng,
            IReadOnlyDictionary<int, Team> teamsById, int humanTeamId = 0)
        {
            var allListings = await _listings.GetBySeasonAsync(season);
            await TransferAiService.RunWeeklyTickAsync(team, allListings, _transferMarket, difficulty, season, currentDate, rng, humanTeamId);
            await TransferAiService.EvaluateIncomingOffersAsync(team, allListings, _transferMarket, teamsById, currentDate, humanTeamId, rng);

            var teamContracts = await _contracts.GetByTeamAsync(team.Id);
            await ContractRenewalAiService.RunWeeklyTickAsync(team, teamContracts, _contracts, currentDate);

            ClubManagementAiService.TryUpgradeStadium(team, rng);
            await ClubManagementAiService.TryHireMissingStaffAsync(team, _staffMarket, currentDate, rng);
        }

        public Task ReturnExpiredLoansAsync(DateTime currentDate, IReadOnlyDictionary<int, Team> teamsById) =>
            _transferMarket.ReturnExpiredLoansAsync(currentDate, teamsById);
    }
}
