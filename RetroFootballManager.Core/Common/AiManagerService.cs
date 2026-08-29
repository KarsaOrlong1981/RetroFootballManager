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
        private readonly PlayerRepository _players;

        public AiManagerService(
            TransferMarketService transferMarket, StaffMarketService staffMarket,
            ContractRepository contracts, TransferListingRepository listings, PlayerRepository players)
        {
            _transferMarket = transferMarket;
            _staffMarket = staffMarket;
            _contracts = contracts;
            _listings = listings;
            _players = players;
        }

        // isTransferWindowOpen: negotiating/bidding is always active (see TransferAiService.
        // RunWeeklyTickAsync) - only completing an accepted deal is gated on the window
        // (EvaluateIncomingOffersAsync), matching real transfer-window rules.
        public async Task RunWeeklyTickAsync(
            Team team, int season, DateTime currentDate, Difficulty difficulty, Random rng,
            IReadOnlyDictionary<int, Team> teamsById, int humanTeamId = 0, int matchdayIndex = 0,
            bool isTransferWindowOpen = true)
        {
            var allListings = await _listings.GetBySeasonAsync(season);
            IReadOnlyDictionary<int, Player>? freeAgentsById = allListings.Any(l => l.IsFreeAgent)
                ? (await _players.GetPlayersByTeamAsync(0)).ToDictionary(p => p.Id)
                : null;

            // Financial self-preservation: tapers every discretionary spend below as the club's
            // own season-end projection worsens, and raises cash outright once things are bad
            // enough - see FinanceAiService. Crisis selling runs unconditionally (every week,
            // not gated by the below-mentioned random activity roll) since it's corrective, not
            // "spare time" activity.
            double cautionFactor = FinanceAiService.ComputeCautionFactor(team, difficulty, matchdayIndex);
            if (cautionFactor <= 0.2)
            {
                var alreadyListedIds = allListings.Where(l => l.TeamId == team.Id).Select(l => l.PlayerId).ToHashSet();
                await FinanceAiService.TryListPlayerForCrisisFundsAsync(team, _transferMarket, alreadyListedIds, season, currentDate);
            }

            await TransferAiService.RunWeeklyTickAsync(
                team, allListings, _transferMarket, difficulty, season, currentDate, rng, humanTeamId, freeAgentsById, cautionFactor, teamsById);
            await TransferAiService.EvaluateIncomingOffersAsync(
                team, allListings, _transferMarket, teamsById, currentDate, humanTeamId, rng, isTransferWindowOpen);

            var teamContracts = await _contracts.GetByTeamAsync(team.Id);
            await ContractRenewalAiService.RunWeeklyTickAsync(team, teamContracts, _contracts, currentDate);

            if (cautionFactor > 0.2)
            {
                ClubManagementAiService.TryUpgradeStadium(team, rng);
                await ClubManagementAiService.TryHireMissingStaffAsync(team, _staffMarket, difficulty, currentDate, rng);
            }
        }

        public Task ReturnExpiredLoansAsync(DateTime currentDate, IReadOnlyDictionary<int, Team> teamsById) =>
            _transferMarket.ReturnExpiredLoansAsync(currentDate, teamsById);
    }
}
