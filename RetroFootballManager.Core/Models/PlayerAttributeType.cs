namespace RetroFootballManager.Models
{
    // Every numeric gameplay attribute on Player, for the scouting-focus attribute filter
    // (Core/Common/PlayerAttributeAccessor.cs reads a Player's value for a given type).
    public enum PlayerAttributeType
    {
        // Outfield
        OffensivePower,
        DefensivePower,
        GameIntelligence,
        PressingIntensity,
        CounterSpeed,
        PassingAccuracy,
        DuelHardness,
        DuelEfficiency,
        CrossingAccuracy,
        HeaderStrength,
        Jumping,
        Dribbling,
        LongShotAccuracy,
        PenaltyKick,
        FreeKick,
        Finishing,
        Positioning,

        // Goalkeeper-specific
        GkReflexes,
        GkHandling,
        GkOneOnOne,
        GkDistribution,
        GkAerialControl,
    }
}
