using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Fires exactly once per transfer-window Open->Closed transition (see
    // GameState.TransferWindowWasOpen) - call from both CalendarAdvanceService's daily tick AND
    // MatchDayService.PlayMatchdayAsync (a matchday advances the date without ever going through
    // CalendarAdvanceService, same dual-hook reasoning as FinanceService.
    // ApplyMonthlySettlementAsync/ClubMembershipService.ApplyCampaignDrip). Restricted to the
    // manager's own league tier per explicit user request - transfers in other leagues aren't
    // reported.
    public static class TransferWindowDigestService
    {
        public static async Task CheckAndSendAsync(
            GameState state, IReadOnlyList<Fixture> seasonFixtures, int managerLeagueTier,
            TransferHistoryRepository history, MessageService? messages)
        {
            bool isOpenToday = SeasonPhaseCalculator.IsTransferWindowOpen(state.CurrentDate, state.MatchdayIndex, seasonFixtures);
            bool wasOpen = state.TransferWindowWasOpen;
            state.TransferWindowWasOpen = isOpenToday;

            if (!wasOpen || isOpenToday || messages is null)
                return;

            var since = state.LastTransferDigestDate ?? DateTime.MinValue;
            state.LastTransferDigestDate = state.CurrentDate;

            var entries = (await history.GetSinceAsync(since))
                .Where(e => e.Date <= state.CurrentDate)
                .Where(e => e.FromLeagueTier == managerLeagueTier || e.ToLeagueTier == managerLeagueTier)
                .OrderBy(e => e.Date)
                .ToList();

            if (entries.Count == 0)
                return;

            // Own transfers get a leading marker (InboxViewModel turns this into a highlighted
            // color for the row - see InboxMessageRow.BodyFormatted) - plain text otherwise,
            // since Inbox messages have no other way to carry per-line color.
            var lines = entries.Select(e =>
            {
                bool isOwn = e.FromTeamId == state.ManagerTeamId || e.ToTeamId == state.ManagerTeamId;
                string marker = isOwn ? "★" : "";
                return $"{marker}{e.PlayerName} ({e.PlayerRating:0.0}) - {e.FromTeamName} → {e.ToTeamName} - {e.Fee:N0} €";
            });

            await messages.SendAsync(MessageType.TransferWindowDigest, "Transferfenster geschlossen",
                string.Join("\n", lines), state.CurrentDate, state.ManagerTeamId);
        }
    }
}
