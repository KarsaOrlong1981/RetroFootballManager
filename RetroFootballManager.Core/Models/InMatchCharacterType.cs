namespace RetroFootballManager.Models
{
    // A player's transient, match-specific temperament - distinct from the permanent
    // Personality enum (trained/talked-about over the season). Deliberately separate systems
    // despite a couple of overlapping names (Hothead, Leader) - see InMatchCharacterDisplay for
    // the diverging display labels that avoid confusing the two in the UI.
    public enum InMatchCharacterType
    {
        Fighter,                // Fights back hardest when behind
        ClutchPerformer,        // Thrives in the closing minutes / high-pressure moments
        Leader,                 // Stabilizes teammates' morale
        MomentumHunter,         // Feeds off a recent goal, snowballs quickly
        IceCold,                // Barely reacts to events either way
        NervousUnderPressure,   // Loses composure when the game is close/tense
        Complacent,             // Drops off when comfortably ahead
        Hothead,                // Prone to losing his temper, higher foul risk when rattled
        FragileConfidence,      // Very sensitive to criticism/setbacks
        LazyWhenLeading,        // Effort drops noticeably once ahead
        Emotional,              // Big swings both ways
        RiskTaker,              // Doesn't calm down, keeps gambling regardless of moral
        CrowdDriven,            // Reacts strongly to home advantage/atmosphere
        SlowStarter,            // Needs time to get going, minor early-match penalty
        MomentumSensitive,      // Strongly affected by the last event, good or bad
    }
}
