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
    }
}
