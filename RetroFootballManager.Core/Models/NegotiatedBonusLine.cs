namespace RetroFootballManager.Models
{
    // One bonus line agreed in a negotiation dialog, held on PendingNegotiation.Bonuses until
    // NegotiationResolutionService materializes it into a ContractBonus row.
    public record NegotiatedBonusLine(ContractBonusType Type, double Amount);
}
