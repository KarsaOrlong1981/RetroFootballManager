using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // AI counterpart to human transfer market activity: COM teams periodically list surplus/weak
    // players and bid on listings from other teams when they can afford it. Activity scales with
    // Difficulty (Easy = rarer/cautious, Hard = more frequent/aggressive).
    public static class TransferAiService
    {
        // Positions with at least this many players are considered overstocked. Deliberately
        // low enough that PlayerGenerator.DefaultPositionPlan's own generation counts (3 GKs,
        // 4 CentralDefenders, 3 Forwards) already qualify - a strict ">4" against a plan that
        // never generates more than 4 of anything meant this could never fire on a fresh squad.
        private const int SurplusThreshold = 3;

        // DirectorOfFootball negotiates better transfer prices - up to +/-15% depending on his
        // Rating. Only one DoF can ever be on staff (see StaffMarketService.MaxEmployeesPerType),
        // so there is no stacking case to consider.
        private const double DirectorOfFootballMaxPriceSwing = 0.15;

        // Public - pure and deterministic, directly unit-testable.
        public static double DirectorOfFootballPriceFactor(Team? team, bool favorSeller)
        {
            var dof = team?.Employees.FirstOrDefault(e => e.EmployeeType == EmployeeType.DirectorOfFootball);
            if (dof is null)
                return 1.0;

            double swing = (dof.Rating / 100.0) * DirectorOfFootballMaxPriceSwing;
            return favorSeller ? 1.0 + swing : 1.0 - swing;
        }

        private static double ActivityChance(Difficulty difficulty) => difficulty switch
        {
            Difficulty.Easy => 0.15,
            Difficulty.Hard => 0.45,
            _ => 0.3,
        };

        // How large a single transfer fee may be relative to CURRENT cash - independent of the
        // season-end trend (cautionFactor), which only reacts to a big one-off fee AFTER it's
        // already been paid. Without this a well-funded team could blow most of its balance on
        // one signing while its trend still looks perfectly healthy. Same difficulty direction as
        // ActivityChance (Hard = most disciplined). cautionFactor still multiplies in on top, so
        // the cap tightens further as the trend actually worsens - not just a single fixed share.
        private static double MaxSingleTransferShareOfBalance(Difficulty difficulty) => difficulty switch
        {
            Difficulty.Hard => 0.25,
            Difficulty.Easy => 0.55,
            _ => 0.4,
        };

        // A team actively looks to recruit once a position drops below this many senior players
        // - mirrors SurplusThreshold's shape from the other direction ("too few" vs "too many").
        private const int MinDepthPerPosition = 2;

        // One AI tick per team and call: at most one new listing + at most one offer.
        // freeAgentsById resolves a free-agent listing's PlayerId to the actual Player (TeamId 0
        // listings have no owning club to look the player up through) - only needed/fetched by
        // the caller when at least one such listing exists (see AiManagerService). teamsById
        // resolves a normal (non-free-agent) listing's player the same way, so the bid-target
        // pick below can actually tell whether a listing fills a real squad need.
        // cautionFactor (see FinanceAiService.ComputeCautionFactor) tapers toward 0 as the
        // team's own finances worsen - selling (the listing half below) is always still allowed
        // (it helps a struggling club), only spending on a bid is suppressed once things are bad
        // enough that taking on more wages would make things worse.
        public static async Task RunWeeklyTickAsync(
            Team team, IReadOnlyList<TransferListing> allListings, TransferMarketService market,
            Difficulty difficulty, int season, DateTime currentDate, Random rng, int humanTeamId = 0,
            IReadOnlyDictionary<int, Player>? freeAgentsById = null, double cautionFactor = 1.0,
            IReadOnlyDictionary<int, Team>? teamsById = null)
        {
            if (rng.NextDouble() > ActivityChance(difficulty))
                return;

            var alreadyListedIds = allListings.Where(l => l.TeamId == team.Id).Select(l => l.PlayerId).ToHashSet();
            var surplus = FindSurplusPlayer(team, alreadyListedIds);
            if (surplus is not null)
            {
                double askingPrice = EstimateMarketValue(surplus) * DirectorOfFootballPriceFactor(team, favorSeller: true);
                await market.ListPlayerAsync(surplus, team, askingPrice, season, currentDate);
            }

            if (cautionFactor <= 0.1)
                return;

            var affordableListings = allListings
                .Where(l => l.TeamId != team.Id && CanAfford(team, l, difficulty, cautionFactor)).ToList();

            Player? ResolvePlayer(TransferListing l) =>
                l.IsFreeAgent && freeAgentsById is not null && freeAgentsById.TryGetValue(l.PlayerId, out var freeAgent) ? freeAgent
                : teamsById is not null && teamsById.TryGetValue(l.TeamId, out var owner) ? owner.Players.FirstOrDefault(p => p.Id == l.PlayerId)
                : null;

            // Positions with too few senior players - a hole in the squad, not just a nice-to-
            // have upgrade. A listing filling one of these is preferred over a random pick, same
            // "scout and recruit when actually needed" idea FindSurplusPlayer already applies to
            // selling. Falls back to the old random pick when no player could be resolved (tests
            // that don't pass teamsById/freeAgentsById) or no listing happens to fill a gap.
            var shortagePositions = team.Players.GroupBy(p => p.Position)
                .Where(g => g.Count() < MinDepthPerPosition)
                .Select(g => g.Key)
                .Concat(Enum.GetValues<Position>().Except(team.Players.Select(p => p.Position)))
                .ToHashSet();

            var target = affordableListings
                .Select(l => (Listing: l, Player: ResolvePlayer(l)))
                .Where(x => x.Player is not null && shortagePositions.Contains(x.Player!.Position))
                .OrderBy(_ => rng.Next())
                .Select(x => x.Listing)
                .FirstOrDefault()
                ?? affordableListings.OrderBy(_ => rng.Next()).FirstOrDefault();
            if (target is not null)
            {
                double fee, wage;
                if (target.IsFreeAgent && freeAgentsById is not null && freeAgentsById.TryGetValue(target.PlayerId, out var freeAgent))
                {
                    // No fee to negotiate - only the wage, based on the player's own market
                    // value (AskingPrice is always 0 for a free agent, so it carries no signal).
                    fee = 0;
                    wage = PlayerValuationService.EstimateAnnualSalary(freeAgent) * (0.9 + rng.NextDouble() * 0.2)
                        * DirectorOfFootballPriceFactor(team, favorSeller: false);
                }
                else
                {
                    fee = target.AskingPrice * (0.85 + rng.NextDouble() * 0.3) * DirectorOfFootballPriceFactor(team, favorSeller: false);
                    // Rough heuristic (no access to the other team's actual player here) -
                    // ~15% of market value as annual salary, matching PlayerValuationService.EstimateAnnualSalary.
                    wage = target.AskingPrice * 0.15;
                }
                await market.MakeOfferAsync(target, team, fee, wage, currentDate, humanTeamId);
            }
        }

        public static double EstimateMarketValue(Player player) => PlayerValuationService.EstimateMarketValue(player);

        // Chance an unsolicited offer (see MakeUnsolicitedOfferAsync) is flatly turned down -
        // "not for sale right now" - instead of getting a counter-fee.
        private const double UnsolicitedFlatRefusalChance = 0.25;

        // Evaluates incoming offers on the team's OWN listings - without this, offers (including
        // the human player's) would stay "pending" forever since no one ever accepts/rejects them.
        // isTransferWindowOpen: negotiating (countering/rejecting) is always allowed regardless -
        // only actually completing a good-enough offer is gated on the window, see
        // SeasonPhaseCalculator.IsTransferWindowOpen. A good offer outside the window is simply
        // left Pending (not rejected, not accepted) and re-evaluated again next week.
        public static async Task EvaluateIncomingOffersAsync(
            Team team, IReadOnlyList<TransferListing> ownListings, TransferMarketService market,
            IReadOnlyDictionary<int, Team> teamsById, DateTime currentDate, int humanTeamId = 0, Random? rng = null,
            bool isTransferWindowOpen = true)
        {
            foreach (var listing in ownListings.Where(l => l.TeamId == team.Id))
            {
                var player = team.Players.FirstOrDefault(p => p.Id == listing.PlayerId);
                if (player is null)
                    continue;

                var offers = await market.GetOffersForListingAsync(listing);
                var pendingOffers = offers.Where(o => o.Status == TransferOfferStatus.Pending).ToList();

                // Offers locked in by the negotiation dialog's Bedenkzeit (see
                // TransferOffer.LockedUntilDate) are left untouched here - neither picked as
                // "best" nor rejected - until NegotiationResolutionService resolves them on
                // their DecisionDate. A rival's un-locked offer on the same listing is still
                // evaluated normally and can win the listing outright in the meantime.
                // Strict "<" (not "<="): on the exact DecisionDate, NegotiationResolutionService
                // always gets first crack at it later the same day - only still-unresolved
                // offers fall back to this normal AI evaluation from the day after.
                var evaluableOffers = pendingOffers.Where(o => o.LockedUntilDate is null || o.LockedUntilDate < currentDate).ToList();
                if (evaluableOffers.Count == 0)
                    continue;

                var bestOffer = evaluableOffers.OrderByDescending(o => o.OfferedFee).First();
                if (!teamsById.TryGetValue(bestOffer.OfferingTeamId, out var buyingTeam))
                    continue;

                if (ShouldAcceptOffer(listing, bestOffer, team, buyingTeam))
                {
                    if (!isTransferWindowOpen)
                        continue; // deal is good, window's closed - leave it pending, retry next week

                    if (listing.IsLoanListing)
                    {
                        await market.LoanOutAsync(player, team, buyingTeam, currentDate, currentDate.AddMonths(6), bestOffer.WageOffer);
                        await market.RemoveListingAsync(listing);
                        foreach (var pending in evaluableOffers)
                            await market.RejectOfferAsync(pending, team, player, "ein anderer Verein hat den Zuschlag erhalten", humanTeamId);
                    }
                    else
                    {
                        // AcceptOfferAsync already deletes the listing + all related offers.
                        await market.AcceptOfferAsync(bestOffer, listing, team, buyingTeam, player, currentDate, humanTeamId);
                    }
                    continue;
                }

                // A club that never listed the player wasn't looking to sell - rather than an
                // instant no, they either flatly refuse or name their own (higher) price.
                if (listing.IsUnsolicited && (rng ?? new Random()).NextDouble() >= UnsolicitedFlatRefusalChance)
                {
                    double counterFee = (listing.IsLoanListing ? listing.AskingPrice * 0.25 : listing.AskingPrice * 1.3)
                        * DirectorOfFootballPriceFactor(team, favorSeller: true);
                    await market.CounterOfferAsync(bestOffer, listing, counterFee, currentDate, humanTeamId);
                    foreach (var other in evaluableOffers.Where(o => o.Id != bestOffer.Id))
                        await market.RejectOfferAsync(other, team, player, "der Verein hat einem anderen Bieter ein Gegenangebot gemacht", humanTeamId);
                    continue;
                }

                string reason = listing.IsUnsolicited
                    ? "der Spieler steht derzeit nicht zum Verkauf"
                    : "das Angebot war dem Verein zu niedrig";
                foreach (var pending in evaluableOffers)
                    await market.RejectOfferAsync(pending, team, player, reason, humanTeamId);
            }
        }

        // Transfer: offer must reach at least 80% of the asking price. Loan: only requires a
        // salary offer at all - full market value isn't relevant for a loan anyway (see
        // TransferMarketService.LoanOutAsync). Unsolicited offers (player never put up for
        // transfer, see TransferMarketService.MakeUnsolicitedOfferAsync) demand a clear premium -
        // the club wasn't looking to sell, so a fair-value bid isn't enough to change their mind.
        // Both sides' DirectorOfFootball adjusts the effective threshold - a good seller-side DoF
        // raises the bar (better sale price), a good buyer-side DoF lowers it (better purchase
        // price) - applied here rather than to the raw fee so it works uniformly for AI- and
        // human-initiated offers alike.
        private static bool ShouldAcceptOffer(TransferListing listing, TransferOffer offer, Team sellingTeam, Team? buyingTeam)
        {
            double factor = DirectorOfFootballPriceFactor(sellingTeam, favorSeller: true)
                * DirectorOfFootballPriceFactor(buyingTeam, favorSeller: false);

            if (listing.IsUnsolicited)
                return listing.IsLoanListing
                    ? offer.WageOffer >= listing.AskingPrice * 0.25 * factor
                    : offer.OfferedFee >= listing.AskingPrice * 1.3 * factor;
            return listing.IsLoanListing ? offer.WageOffer > 0 : offer.OfferedFee >= listing.AskingPrice * 0.8 * factor;
        }

        private static Player? FindSurplusPlayer(Team team, HashSet<int> alreadyListedIds)
        {
            foreach (var group in team.Players.Where(p => !alreadyListedIds.Contains(p.Id)).GroupBy(p => p.Position))
            {
                if (group.Count() >= SurplusThreshold)
                    return group.OrderBy(p => p.Rating).First();
            }
            return null;
        }

        private static bool CanAfford(Team team, TransferListing listing, Difficulty difficulty, double cautionFactor)
        {
            if (team.Finances is null || !FinanceService.HasSpendableBalance(team))
                return false;

            double requiredMargin = listing.AskingPrice * 1.2;
            double maxSingleSpend = team.Finances.CurrentBalance
                * MaxSingleTransferShareOfBalance(difficulty) * Math.Clamp(cautionFactor, 0.0, 1.0);
            return requiredMargin <= maxSingleSpend;
        }
    }
}
