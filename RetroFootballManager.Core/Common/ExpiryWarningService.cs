using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Warns the user's team about expiring player contracts and loans (60/30/14 days ahead,
    // once per threshold - see MessageService.HasWarnedAsync).
    public class ExpiryWarningService
    {
        private static readonly int[] ThresholdsDays = [60, 30, 14];

        private readonly ContractRepository _contracts;
        private readonly LoanAgreementRepository _loans;
        private readonly MessageService _messages;

        public ExpiryWarningService(ContractRepository contracts, LoanAgreementRepository loans, MessageService messages)
        {
            _contracts = contracts;
            _loans = loans;
            _messages = messages;
        }

        public async Task CheckAsync(Team humanTeam, DateTime currentDate)
        {
            var contracts = await _contracts.GetByTeamAsync(humanTeam.Id);
            foreach (var contract in contracts.Where(c => c.HolderType == ContractHolderType.Player))
                await WarnIfDueAsync(
                    MessageType.ContractExpiringSoon, "Vertrag läuft bald aus", "Vertrag von",
                    contract.HolderId, contract.EndDate, humanTeam, currentDate);

            var loans = await _loans.GetByTeamAsync(humanTeam.Id);
            foreach (var loan in loans.Where(l => l.LoanTeamId == humanTeam.Id))
                await WarnIfDueAsync(
                    MessageType.LoanExpiringSoon, "Leihe läuft bald aus", "Leihe von",
                    loan.PlayerId, loan.EndDate, humanTeam, currentDate);
        }

        private async Task WarnIfDueAsync(
            MessageType type, string title, string bodyPrefix, int playerId, DateTime endDate, Team humanTeam, DateTime currentDate)
        {
            int daysLeft = (endDate.Date - currentDate.Date).Days;
            if (daysLeft < 0)
                return;

            // The tightest (smallest) threshold that still applies - otherwise 60/30/14 would
            // all fire one after another on consecutive days once daysLeft is already below
            // the largest threshold.
            bool anyThreshold = false;
            int closestThreshold = int.MaxValue;
            foreach (var threshold in ThresholdsDays)
            {
                if (daysLeft <= threshold && threshold < closestThreshold)
                {
                    closestThreshold = threshold;
                    anyThreshold = true;
                }
            }
            if (!anyThreshold || await _messages.HasWarnedAsync(playerId, type, closestThreshold))
                return;

            var player = humanTeam.Players.FirstOrDefault(p => p.Id == playerId);
            string name = player?.Name ?? "einem Spieler";
            await _messages.SendAsync(type, title, $"{bodyPrefix} {name} läuft in {daysLeft} Tagen aus.",
                currentDate, humanTeam.Id, playerId, closestThreshold);
        }
    }
}
