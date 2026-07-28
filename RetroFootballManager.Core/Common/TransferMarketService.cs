using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Transfer market: list players, make/accept/reject offers, loans. Usable by both human
    // and AI (see TransferAiService for the AI decision logic).
    public class TransferMarketService
    {
        private readonly TransferListingRepository _listings;
        private readonly TransferOfferRepository _offers;
        private readonly LoanAgreementRepository _loans;
        private readonly TeamRepository _teams;
        private readonly ContractRepository _contracts;
        private readonly MessageService? _messages;

        public TransferMarketService(
            TransferListingRepository listings, TransferOfferRepository offers, LoanAgreementRepository loans,
            TeamRepository teams, ContractRepository contracts, MessageService? messages = null)
        {
            _listings = listings;
            _offers = offers;
            _loans = loans;
            _teams = teams;
            _contracts = contracts;
            _messages = messages;
        }

        public async Task<TransferListing> ListPlayerAsync(
            Player player, Team team, double askingPrice, int season, DateTime date, bool isLoanListing = false)
        {
            var listing = new TransferListing
            {
                PlayerId = player.Id,
                TeamId = team.Id,
                AskingPrice = askingPrice,
                IsLoanListing = isLoanListing,
                Season = season,
                ListedDate = date,
            };
            await _listings.SaveAsync(listing);
            return listing;
        }

        public Task RemoveListingAsync(TransferListing listing) => _listings.DeleteAsync(listing.Id);

        public async Task<TransferOffer> MakeOfferAsync(
            TransferListing listing, Team offeringTeam, double fee, double wageOffer, DateTime date, int humanTeamId = 0)
        {
            var offer = new TransferOffer
            {
                ListingId = listing.Id,
                OfferingTeamId = offeringTeam.Id,
                OfferedFee = fee,
                WageOffer = wageOffer,
                Status = TransferOfferStatus.Pending,
                CreatedDate = date,
            };
            await _offers.SaveAsync(offer);

            if (_messages is not null && humanTeamId != 0 && listing.TeamId == humanTeamId && offeringTeam.Id != humanTeamId)
            {
                var sellingTeam = await _teams.GetTeamAsync(listing.TeamId);
                var player = sellingTeam?.Players.FirstOrDefault(p => p.Id == listing.PlayerId);
                await _messages.SendAsync(MessageType.TransferOfferReceived, "Transferangebot erhalten",
                    $"{offeringTeam.Name} bietet {fee:N0} für {(player?.Name ?? "einen deiner Spieler")}.",
                    date, humanTeamId, listing.PlayerId);
            }

            return offer;
        }

        public Task<List<TransferOffer>> GetOffersForListingAsync(TransferListing listing) =>
            _offers.GetByListingAsync(listing.Id);

        // Unsolicited offer for a player found via scouting, never put up for transfer by their
        // club. Creates a shadow listing (not shown on the public market) so the seller's normal
        // weekly AI evaluation (TransferAiService.EvaluateIncomingOffersAsync) picks it up and the
        // COM manager decides whether to let the player go - at a higher price than a regular sale.
        public async Task<TransferOffer> MakeUnsolicitedOfferAsync(
            Player player, Team sellingTeam, Team offeringTeam, double fee, double wageOffer, int season, DateTime date,
            bool isLoanOffer)
        {
            var listing = new TransferListing
            {
                PlayerId = player.Id,
                TeamId = sellingTeam.Id,
                AskingPrice = TransferAiService.EstimateMarketValue(player),
                IsLoanListing = isLoanOffer,
                Season = season,
                ListedDate = date,
                IsUnsolicited = true,
            };
            await _listings.SaveAsync(listing);
            return await MakeOfferAsync(listing, offeringTeam, fee, wageOffer, date);
        }

        // Seller wants more than offered: park the offer as Countered (see TransferAiService) and
        // let the buyer accept (AcceptCounterOfferAsync) or reject (RejectOfferAsync) it - the AI
        // never re-evaluates a Countered offer itself.
        public async Task CounterOfferAsync(TransferOffer offer, TransferListing listing, double counterFee, DateTime date, int humanTeamId = 0)
        {
            offer.Status = TransferOfferStatus.Countered;
            offer.CounterFee = counterFee;
            await _offers.SaveAsync(offer);

            if (_messages is not null && humanTeamId != 0 && offer.OfferingTeamId == humanTeamId)
            {
                var sellingTeam = await _teams.GetTeamAsync(listing.TeamId);
                var player = sellingTeam?.Players.FirstOrDefault(p => p.Id == listing.PlayerId);
                await _messages.SendAsync(MessageType.TransferOfferCountered, "Gegenangebot erhalten",
                    $"{sellingTeam?.Name ?? "Der Verein"} verlangt {counterFee:N0} für {(player?.Name ?? "den Spieler")}.",
                    date, humanTeamId, listing.PlayerId);
            }
        }

        // Buyer agrees to the seller's counter-fee - finalizes the transfer/loan at that fee.
        public async Task AcceptCounterOfferAsync(
            TransferOffer offer, TransferListing listing, Team sellingTeam, Team buyingTeam, Player player, DateTime date,
            int humanTeamId = 0)
        {
            if (listing.IsLoanListing)
            {
                await LoanOutAsync(player, sellingTeam, buyingTeam, date, date.AddMonths(6), offer.CounterFee);
                await RemoveListingAsync(listing);
                offer.Status = TransferOfferStatus.Accepted;
                await _offers.SaveAsync(offer);
                await _offers.DeleteByListingAsync(listing.Id);
            }
            else
            {
                offer.OfferedFee = offer.CounterFee;
                await AcceptOfferAsync(offer, listing, sellingTeam, buyingTeam, player, date, humanTeamId);
            }
        }

        // sellingTeam/player/reason are optional so the human's own "reject this incoming
        // offer" action (where no message is needed - the human already knows) can keep
        // calling this with just the offer, same as before.
        public async Task RejectOfferAsync(
            TransferOffer offer, Team? sellingTeam = null, Player? player = null, string? reason = null,
            int humanTeamId = 0)
        {
            offer.Status = TransferOfferStatus.Rejected;
            await _offers.SaveAsync(offer);

            if (_messages is not null && humanTeamId != 0 && offer.OfferingTeamId == humanTeamId)
            {
                string clubName = sellingTeam?.Name ?? "Der Verein";
                string playerName = player?.Name ?? "den Spieler";
                string reasonSuffix = reason is not null ? $" ({reason})" : "";
                await _messages.SendAsync(MessageType.TransferOfferRejected, "Angebot abgelehnt",
                    $"{clubName} hat dein Angebot für {playerName} abgelehnt{reasonSuffix}.",
                    offer.CreatedDate, humanTeamId, player?.Id);
            }
        }

        // Completes the transfer: player moves squad, new contract at the buyer (transfer fee
        // is not used for salary calculation - only WageOffer does that), fee flows into
        // TransferIncome/-Expense of both clubs.
        public async Task AcceptOfferAsync(
            TransferOffer offer, TransferListing listing, Team sellingTeam, Team buyingTeam, Player player, DateTime date,
            int humanTeamId = 0)
        {
            sellingTeam.Players.Remove(player);
            player.TeamId = buyingTeam.Id;
            buyingTeam.Players.Add(player);

            int fee = (int)Math.Round(offer.OfferedFee);
            if (sellingTeam.Finances is not null)
            {
                sellingTeam.Finances.TransferIncome += fee;
                sellingTeam.Finances.CurrentBalance += fee;
            }
            if (buyingTeam.Finances is not null)
            {
                buyingTeam.Finances.TransferExpense += fee;
                buyingTeam.Finances.CurrentBalance -= fee;
            }

            await _contracts.SaveAsync(new Contract
            {
                HolderId = player.Id,
                HolderType = ContractHolderType.Player,
                TeamId = buyingTeam.Id,
                StartDate = date,
                EndDate = date.AddYears(3),
                AnnualSalary = offer.WageOffer,
                MarketValue = offer.OfferedFee,
            });

            offer.Status = TransferOfferStatus.Accepted;
            await _offers.SaveAsync(offer);
            await _offers.DeleteByListingAsync(listing.Id);
            await _listings.DeleteAsync(listing.Id);

            await _teams.SaveTeamAsync(sellingTeam, includeYouth: false);
            await _teams.SaveTeamAsync(buyingTeam, includeYouth: false);

            if (_messages is not null && humanTeamId != 0 && offer.OfferingTeamId == humanTeamId)
            {
                await _messages.SendAsync(MessageType.TransferOfferAccepted, "Angebot angenommen",
                    $"Dein Angebot für {player.Name} wurde angenommen.", date, humanTeamId, player.Id);
            }
        }

        // On a loan, the loan club only takes on the negotiated salary (negotiatedWage), not the
        // market value - the existing player contract moves to the loan club for the loan
        // duration (TeamId + salary changed) and is reset on return.
        public async Task<LoanAgreement> LoanOutAsync(
            Player player, Team originTeam, Team loanTeam, DateTime start, DateTime end, double negotiatedWage)
        {
            originTeam.Players.Remove(player);
            player.TeamId = loanTeam.Id;
            loanTeam.Players.Add(player);

            var contracts = await _contracts.GetByHolderAsync(player.Id, ContractHolderType.Player);
            var activeContract = PlayerContractService.GetActiveContract(player.Id, contracts, start);

            var loan = new LoanAgreement
            {
                PlayerId = player.Id,
                OriginTeamId = originTeam.Id,
                LoanTeamId = loanTeam.Id,
                StartDate = start,
                EndDate = end,
                Returned = false,
                ContractId = activeContract?.Id ?? 0,
                OriginalAnnualSalary = activeContract?.AnnualSalary ?? 0,
            };
            await _loans.SaveAsync(loan);

            if (activeContract is not null)
            {
                activeContract.TeamId = loanTeam.Id;
                activeContract.AnnualSalary = negotiatedWage;
                await _contracts.SaveAsync(activeContract);
            }

            await _teams.SaveTeamAsync(originTeam, includeYouth: false);
            await _teams.SaveTeamAsync(loanTeam, includeYouth: false);
            return loan;
        }

        // Ensures at least minimumCount players from foreign (non-German) clubs are always on
        // the market, even if no AI team has listed anything yet (e.g. right at season start) -
        // otherwise the market would be empty initially.
        // Foreign clubs = LeagueTier 0 (the fictional CL/Europa Cup clubs, M6c/M6d).
        public async Task EnsureMinimumForeignListingsAsync(
            IReadOnlyList<Team> allTeams, int season, DateTime currentDate, int minimumCount, Random rng)
        {
            var foreignTeams = allTeams.Where(t => t.LeagueTier == 0).ToList();
            if (foreignTeams.Count == 0)
                return;

            var existing = await _listings.GetBySeasonAsync(season);
            var alreadyListedPlayerIds = existing.Select(l => l.PlayerId).ToHashSet();
            var foreignTeamIds = foreignTeams.Select(t => t.Id).ToHashSet();
            int currentForeignListings = existing.Count(l => foreignTeamIds.Contains(l.TeamId));
            int needed = minimumCount - currentForeignListings;
            if (needed <= 0)
                return;

            var candidates = foreignTeams
                .SelectMany(t => t.Players.Select(p => (Team: t, Player: p)))
                .Where(x => !alreadyListedPlayerIds.Contains(x.Player.Id))
                .OrderBy(_ => rng.Next())
                .Take(needed)
                .ToList();

            foreach (var (team, player) in candidates)
            {
                bool isLoan = rng.NextDouble() < 0.5;
                double price = TransferAiService.EstimateMarketValue(player);
                await ListPlayerAsync(player, team, price, season, currentDate, isLoan);
            }
        }

        // Automatically returns loaned-out players to their origin club after expiry - call
        // weekly from the matchday tick (analogous to PlayerContractService).
        public async Task ReturnExpiredLoansAsync(DateTime currentDate, IReadOnlyDictionary<int, Team> teamsById)
        {
            var active = await _loans.GetActiveAsync();
            foreach (var loan in active.Where(l => l.EndDate <= currentDate))
            {
                if (!teamsById.TryGetValue(loan.LoanTeamId, out var loanTeam)
                    || !teamsById.TryGetValue(loan.OriginTeamId, out var originTeam))
                    continue;

                var player = loanTeam.Players.FirstOrDefault(p => p.Id == loan.PlayerId);
                if (player is not null)
                {
                    loanTeam.Players.Remove(player);
                    player.TeamId = originTeam.Id;
                    originTeam.Players.Add(player);
                    await _teams.SaveTeamAsync(loanTeam, includeYouth: false);
                    await _teams.SaveTeamAsync(originTeam, includeYouth: false);
                }

                if (loan.ContractId != 0)
                {
                    var contract = await _contracts.GetByIdAsync(loan.ContractId);
                    if (contract is not null)
                    {
                        contract.TeamId = loan.OriginTeamId;
                        contract.AnnualSalary = loan.OriginalAnnualSalary;
                        await _contracts.SaveAsync(contract);
                    }
                }

                loan.Returned = true;
                await _loans.SaveAsync(loan);
            }
        }
    }
}
