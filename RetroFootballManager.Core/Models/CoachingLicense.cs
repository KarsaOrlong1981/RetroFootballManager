namespace RetroFootballManager.Models
{
    // Coaching license tier, directly derived from CareerService.HighestUnlockedTier -
    // no separate currency/progression, see ManagerProfileGenerator.
    public enum CoachingLicense
    {
        C,
        B,
        A,
        Pro,
    }
}
