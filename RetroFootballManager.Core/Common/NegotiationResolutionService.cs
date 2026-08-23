using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Resolves PendingNegotiation rows once their "Bedenkzeit" (think-it-over period)
    // DecisionDate is reached - called daily from CalendarAdvanceService, mirrors
    // SaveGameService.ApplyDueScoutingAsync (a due-date-driven daily resolver).
    public class NegotiationResolutionService
    {
        private readonly PendingNegotiationRepository _pending;
        private readonly TransferOfferRepository _offers;
        private readonly TransferListingRepository _listings;
        private readonly ContractRepository _contracts;
        private readonly ContractBonusRepository _bonuses;
        private readonly TransferMarketService _market;
        private readonly MessageService _messages;

        public NegotiationResolutionService(
            PendingNegotiationRepository pending, TransferOfferRepository offers, TransferListingRepository listings,
            ContractRepository contracts, ContractBonusRepository bonuses, TransferMarketService market,
            MessageService messages)
        {
            _pending = pending;
            _offers = offers;
            _listings = listings;
            _contracts = contracts;
            _bonuses = bonuses;
            _market = market;
            _messages = messages;
        }

        public async Task ApplyDueNegotiationsAsync(
            int teamId, DateTime currentDate, IReadOnlyDictionary<int, Team> teamsById)
        {
            var due = await _pending.GetDueAsync(teamId, currentDate);
            foreach (var negotiation in due)
            {
                if (negotiation.Kind == NegotiationKind.ContractRenewal)
                    await ResolveRenewalAsync(negotiation, teamsById, currentDate);
                else
                    await ResolveTransferOrLoanAsync(negotiation, teamsById, currentDate);

                await _pending.DeleteAsync(negotiation.Id);
            }
        }

        private async Task ResolveTransferOrLoanAsync(
            PendingNegotiation negotiation, IReadOnlyDictionary<int, Team> teamsById, DateTime currentDate)
        {
            var offer = negotiation.TransferOfferId.HasValue
                ? await _offers.GetByIdAsync(negotiation.TransferOfferId.Value)
                : null;

            TransferListing? listing = offer is not null ? await _listings.GetByIdAsync(offer.ListingId) : null;
            Team? sellingTeam = listing is not null ? teamsById.GetValueOrDefault(listing.TeamId) : null;
            Player? player = sellingTeam?.Players.FirstOrDefault(p => p.Id == negotiation.PlayerId);

            if (offer is null || offer.Status != TransferOfferStatus.Pending || listing is null
                || sellingTeam is null || player is null || !teamsById.TryGetValue(negotiation.TeamId, out var buyingTeam))
            {
                await SendOutbidMessageAsync(negotiation, teamsById, currentDate);
                return;
            }

            if (listing.IsLoanListing)
            {
                // A loan only ever negotiates wage - role/bonuses/exit clause/sell-on% would
                // outlive the loan on the origin club's contract, since
                // TransferMarketService.ReturnExpiredLoansAsync only reverts TeamId/AnnualSalary.
                int months = negotiation.LoanDurationMonths ?? 6;
                await _market.LoanOutAsync(player, sellingTeam, buyingTeam, currentDate, currentDate.AddMonths(months), offer.WageOffer);
                await _market.RemoveListingAsync(listing);
            }
            else
            {
                if (!TransferMarketService.CanAffordFee(buyingTeam, offer.OfferedFee))
                {
                    // Reject outright (not just skip) - otherwise the now-unlocked offer would
                    // still sit Pending and could get picked up by the normal weekly AI
                    // evaluation later, which never checks the buyer's balance at all.
                    await _market.RejectOfferAsync(offer, sellingTeam, player, "die Ablöse war nicht finanzierbar");
                    await _messages.SendAsync(MessageType.TransferOfferRejected, "Transfer nicht finanzierbar",
                        $"{player.Name} wollte wechseln, aber die Ablöse von {offer.OfferedFee:N0} € hätte die Kontobilanz zu weit ins Minus gedrückt - der Transfer wurde abgesagt.",
                        currentDate, relatedPlayerId: player.Id);
                    return;
                }

                await _market.AcceptOfferAsync(offer, listing, sellingTeam, buyingTeam, player, currentDate);

                var buyerContracts = await _contracts.GetByHolderAsync(player.Id, ContractHolderType.Player);
                var newContract = PlayerContractService.GetActiveContract(player.Id, buyerContracts, currentDate);
                if (newContract is not null)
                {
                    newContract.EndDate = currentDate.AddYears(Math.Max(negotiation.ContractYears, 1));
                    await ApplyNegotiatedTermsAsync(newContract, negotiation);
                }
            }

            await _messages.SendAsync(MessageType.TransferOfferAccepted, "Spieler verpflichtet",
                $"{player.Name} hat nach der Bedenkzeit zugesagt und wechselt zu {buyingTeam.Name}.", currentDate,
                relatedTeamId: sellingTeam.Id, relatedPlayerId: player.Id);
        }

        private async Task ResolveRenewalAsync(
            PendingNegotiation negotiation, IReadOnlyDictionary<int, Team> teamsById, DateTime currentDate)
        {
            var team = teamsById.GetValueOrDefault(negotiation.TeamId);
            var player = team?.Players.FirstOrDefault(p => p.Id == negotiation.PlayerId);
            var contract = negotiation.ContractId.HasValue ? await _contracts.GetByIdAsync(negotiation.ContractId.Value) : null;

            if (player is null || contract is null)
            {
                await _messages.SendAsync(MessageType.TransferOfferRejected, "Vertragsverlängerung nicht zustande gekommen",
                    "Die Verlängerung konnte nicht abgeschlossen werden.", currentDate, relatedPlayerId: negotiation.PlayerId);
                return;
            }

            PlayerContractService.RenewContract(contract, negotiation.NegotiatedWage, negotiation.ContractYears);
            await ApplyNegotiatedTermsAsync(contract, negotiation);

            await _messages.SendAsync(MessageType.ContractRenewed, "Vertrag verlängert",
                $"{player.Name} hat nach der Bedenkzeit den neuen Vertrag unterschrieben.", currentDate,
                relatedTeamId: team!.Id, relatedPlayerId: player.Id);
        }

        private async Task ApplyNegotiatedTermsAsync(Contract contract, PendingNegotiation negotiation)
        {
            contract.RoleInTeam = negotiation.RoleInTeam;
            contract.ReleaseClause = negotiation.ExitClauseAmount;
            contract.SellOnPercentage = negotiation.SellOnPercentage;
            contract.HasNegotiatedTerms = true;
            await _contracts.SaveAsync(contract);

            await _bonuses.DeleteByContractAsync(contract.Id);
            foreach (var bonus in negotiation.Bonuses)
                await _bonuses.SaveAsync(new ContractBonus { ContractId = contract.Id, BonusType = bonus.Type, Amount = bonus.Amount });
        }

        private async Task SendOutbidMessageAsync(
            PendingNegotiation negotiation, IReadOnlyDictionary<int, Team> teamsById, DateTime currentDate)
        {
            string name = teamsById.Values.SelectMany(t => t.Players)
                .FirstOrDefault(p => p.Id == negotiation.PlayerId)?.Name ?? "Der Spieler";
            await _messages.SendAsync(MessageType.TransferOfferRejected, "Transfer nicht zustande gekommen",
                $"{name} hat sich während der Bedenkzeit für ein anderes Angebot entschieden.", currentDate,
                relatedPlayerId: negotiation.PlayerId);
        }
    }
}
