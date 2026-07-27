namespace RetroFootballManager.Models
{
    public enum PlayerStatus
    {
        Available,
        Injured,
        Suspended,
        Tired,
        OnBench,
        InStartingXI,
        // Substituted off during the current match (out, cannot return).
        SubstitutedOff
    }
}
