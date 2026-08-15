namespace RetroFootballManager.Models
{
    // The 10 half-time team-talk options (single source of truth for both the human UI and
    // AiMatchCoach - see TeamTalkService).
    public enum TeamTalkOption
    {
        StayCalm,
        CheerOn,
        Praise,
        Criticize,
        Shout,
        TacticalTalk,
        ExpressConfidence,
        WarnAgainstComplacency,
        EmotionalBuildUp,
        SayNothing,
    }
}
