namespace RetroFootballManager.Models
{
    public enum MessageType
    {
        TransferOfferReceived,
        TransferOfferAccepted,
        TransferOfferRejected,
        TransferOfferCountered,
        ContractExpiringSoon,
        LoanExpiringSoon,

        // A player's contract actually reached EndDate without renewal - see FreeAgentService.
        // Distinct from ContractExpiringSoon (the earlier 60/30/14-day warnings).
        ContractExpired,
        PlayerInjured,
        PlayerRecovered,
        FinanceWarning,
        CalendarAdvanceSummary,
        TrainingCampFinished,
        ScoutingCompleted,
        OpponentAnalysis,
        ClubMoodWarning,
        BoardUltimatum,
        BoardPraise,

        // A negotiation dialog's terms were confirmed after the Bedenkzeit wait - see
        // NegotiationResolutionService. Renewal-only; the transfer/loan case reuses
        // TransferOfferAccepted/TransferOfferRejected.
        ContractRenewed,

        // Club membership count changed - see ClubMembershipService.
        ClubMembershipUpdate,
    }
}
