using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // AI counterpart to the human-only finance-crisis machinery (FinanceService.
    // CheckSeasonEndProjectionAsync, Phase 9) - a COM team gets no board-ultimatum mail (it
    // doesn't read messages), but it still needs to react to a worsening trajectory instead of
    // spending straight into the ground. ComputeCautionFactor feeds a 0-1 multiplier into every
    // other AI discretionary spend (transfers, staff, stadium - see ClubManagementAiService/
    // TransferAiService); TryListPlayerForCrisisFunds is the corrective action once spending
    // restraint alone is no longer enough.
    public static class FinanceAiService
    {
        // How much of the way toward FinanceService.FinancialCrisisThreshold a team needs to be
        // before caution starts tapering in at all - Hard reacts soonest (a well-run AI is a
        // tougher opponent), Easy reacts latest, mirroring TransferAiService.ActivityChance's
        // existing Easy-is-worse/Hard-is-better direction.
        private static double CautionStartFactor(Difficulty difficulty) => difficulty switch
        {
            Difficulty.Hard => 0.4,
            Difficulty.Easy => 0.9,
            _ => 0.65,
        };

        // 1.0 = spend freely, tapering linearly to 0.0 once the season-end projection reaches
        // FinanceService.FinancialCrisisThreshold. Reacts from matchdaysPlayed 0 onward -
        // preseason settlements already count toward the projection.
        public static double ComputeCautionFactor(Team team, Difficulty difficulty, int matchdaysPlayed)
        {
            if (team.Finances is null)
                return 1.0;

            int projected = FinanceService.EstimateSeasonEndBalance(team.Finances, matchdaysPlayed, team.ActiveLoan);

            double crisisLine = FinanceService.FinancialCrisisThreshold;
            double startAt = crisisLine * (1 - CautionStartFactor(difficulty));
            if (projected >= startAt)
                return 1.0;
            if (projected <= crisisLine)
                return 0.0;

            return (projected - crisisLine) / (startAt - crisisLine);
        }

        // Lists the squad's weakest non-critical player once things are bad enough that spending
        // restraint alone won't help - raising cash takes priority over the normal surplus-by-
        // position listing (TransferAiService.FindSurplusPlayer). Never a goalkeeper (a squad
        // down to too few keepers can't field a legal XI); only ever lists one player at a time,
        // same cadence as the surplus path.
        public static async Task<Player?> TryListPlayerForCrisisFundsAsync(
            Team team, TransferMarketService market, HashSet<int> alreadyListedIds, int season, DateTime currentDate)
        {
            var candidate = team.Players
                .Where(p => !alreadyListedIds.Contains(p.Id) && p.Position != Position.Goalkeeper)
                .OrderBy(p => p.Rating)
                .FirstOrDefault();
            if (candidate is null)
                return null;

            double askingPrice = TransferAiService.EstimateMarketValue(candidate)
                * TransferAiService.SellingPriceFactor(team);
            await market.ListPlayerAsync(candidate, team, askingPrice, season, currentDate);
            return candidate;
        }
    }
}
