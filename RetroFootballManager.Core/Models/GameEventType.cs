namespace RetroFootballManager.Models
{
    public enum GameEventType
    {
        KickOff,            // Kick-off
        HalfTime,           // Half-time whistle
        FullTime,           // Full-time whistle

        ChanceCreated,      // Scoring chance created
        Shot,               // Shot
        ShotOnTarget,       // Shot on target
        Goal,               // Goal

        Foul,               // Foul
        YellowCard,         // Yellow card
        RedCard,            // Red card

        Injury,             // Injury
        Substitution,       // Substitution

        Offside,            // Offside
        Corner,             // Corner
        FreeKick,           // Free kick
        Penalty,            // Penalty

        DangerousAttack,    // Dangerous attack
        PossessionChange,   // Possession changes

        Save,               // Goalkeeper save
        GoalKick,           // Goal kick
        ThrowIn,            // Throw-in

        CounterAttack,      // Counter-attack
        BuildUpPlay,        // Build-up play
        PressingAction,     // Pressing action

        TacticChange        // Tactic change (e.g. by the computer opponent)
    }
}
