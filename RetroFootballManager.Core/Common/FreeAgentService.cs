using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Contract expiry: a player whose contract reaches EndDate without renewal is released and
    // listed ablösefrei (fee-free) - for the human team AND every AI team alike (see
    // ContractRenewalAiService for the "renew instead" side of the same weekly window). Only the
    // wage gets negotiated afterwards - see TransferMarketService.SignFreeAgentAsync and
    // NegotiationResolutionService's free-agent branch (human) / EvaluateOffersAsync below (AI).
    public static class FreeAgentService
    {
        // Minimum PlayerTermsExpectationService satisfaction score for a free agent to accept a
        // club's wage offer - same "Neutral" cutoff PlayerTermsExpectationService.EvaluateMood uses.
        private const double MinAcceptSatisfaction = 50;

        // Run daily, for every team (human included): releases any player whose contract has
        // actually expired (renewal happens earlier - see ContractRenewalAiService/the manual
        // renew action - so a contract reaching EndDate here was deliberately let go).
        public static async Task ReleaseExpiredContractsAsync(
            IReadOnlyList<Team> teams, ContractRepository contracts, TransferListingRepository listings,
            PlayerRepository players, MessageService? messages, int season, DateTime currentDate, int humanTeamId)
        {
            foreach (var team in teams)
            {
                var expired = (await contracts.GetByTeamAsync(team.Id))
                    .Where(c => c.HolderType == ContractHolderType.Player && c.EndDate <= currentDate)
                    .ToList();
                if (expired.Count == 0)
                    continue;

                foreach (var contract in expired)
                {
                    var player = team.Players.FirstOrDefault(p => p.Id == contract.HolderId);
                    if (player is null)
                    {
                        // Already gone (sold/loaned since) - the contract row is just stale.
                        await contracts.DeleteAsync(contract.Id);
                        continue;
                    }

                    team.Players.Remove(player);
                    LineupSelector.RefillBench(team);
                    player.TeamId = 0;
                    await players.SavePlayerAsync(player);
                    await contracts.DeleteAsync(contract.Id);
                    await listings.SaveAsync(new TransferListing
                    {
                        PlayerId = player.Id,
                        TeamId = 0,
                        AskingPrice = 0,
                        Season = season,
                        ListedDate = currentDate,
                        IsFreeAgent = true,
                    });

                    if (messages is not null && team.Id == humanTeamId)
                    {
                        await messages.SendAsync(MessageType.ContractExpired, "Vertrag ausgelaufen",
                            $"Der Vertrag von {player.Name} ist ausgelaufen - er ist ab sofort ablösefrei.",
                            currentDate, humanTeamId, player.Id);
                    }
                }
            }
        }

        // Run weekly (once, not per-team): resolves pending offers on every free-agent listing -
        // AI bids already flow in via TransferAiService.RunWeeklyTickAsync (a free-agent listing
        // is just another listing with TeamId 0/AskingPrice 0 to bid on); a human offer stays
        // untouched here while its own Bedenkzeit is running (see the LockedUntilDate check,
        // same convention as TransferAiService.EvaluateIncomingOffersAsync) and is instead
        // resolved by NegotiationResolutionService on its DecisionDate.
        public static async Task EvaluateOffersAsync(
            IReadOnlyDictionary<int, Team> teamsById, PlayerRepository players, TransferListingRepository listings,
            TransferOfferRepository offers, TransferMarketService market, MessageService? messages,
            DateTime currentDate)
        {
            foreach (var listing in await listings.GetFreeAgentsAsync())
            {
                var player = await players.GetPlayerAsync(listing.PlayerId);
                if (player is null)
                {
                    await listings.DeleteAsync(listing.Id);
                    continue;
                }

                var pending = (await offers.GetByListingAsync(listing.Id))
                    .Where(o => o.Status == TransferOfferStatus.Pending
                        && (o.LockedUntilDate is null || o.LockedUntilDate < currentDate)
                        && teamsById.ContainsKey(o.OfferingTeamId))
                    .ToList();
                if (pending.Count == 0)
                    continue;

                var scored = pending
                    .Select(o => (Offer: o, Score: PlayerTermsExpectationService.EstimateSatisfaction(
                        player, teamsById[o.OfferingTeamId].AverageRating, o.WageOffer,
                        RoleInTeam.RotationPlayer, PlayerTermsExpectationService.EstimatePreferredContractYears(player.Age),
                        hasExitClause: false, totalAnnualBonusValue: 0)))
                    .OrderByDescending(x => x.Score)
                    .ToList();

                var best = scored[0];
                if (best.Score >= MinAcceptSatisfaction)
                {
                    var buyingTeam = teamsById[best.Offer.OfferingTeamId];
                    int years = PlayerTermsExpectationService.EstimatePreferredContractYears(player.Age);
                    await market.SignFreeAgentAsync(player, listing, buyingTeam, best.Offer.WageOffer, years, currentDate);

                    foreach (var (offer, _) in scored.Skip(1))
                    {
                        offer.Status = TransferOfferStatus.Rejected;
                        await offers.SaveAsync(offer);
                    }

                    if (messages is not null)
                    {
                        await messages.SendAsync(MessageType.TransferOfferAccepted, "Spieler verpflichtet",
                            $"{player.Name} hat unterschrieben und wechselt ablösefrei zu {buyingTeam.Name}.",
                            currentDate, relatedPlayerId: player.Id);
                    }
                }
                else
                {
                    foreach (var (offer, _) in scored)
                    {
                        offer.Status = TransferOfferStatus.Rejected;
                        await offers.SaveAsync(offer);
                    }
                }
            }
        }
    }
}
