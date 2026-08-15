using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Generic read access to any Player attribute by PlayerAttributeType - used by the
    // scouting-focus attribute filter (ScoutingService.FindCandidatesForFocus).
    public static class PlayerAttributeAccessor
    {
        public static int GetValue(Player player, PlayerAttributeType type) => type switch
        {
            PlayerAttributeType.OffensivePower => player.OffensivePower,
            PlayerAttributeType.DefensivePower => player.DefensivePower,
            PlayerAttributeType.GameIntelligence => player.GameIntelligence,
            PlayerAttributeType.PressingIntensity => player.PressingIntensity,
            PlayerAttributeType.CounterSpeed => player.CounterSpeed,
            PlayerAttributeType.PassingAccuracy => player.PassingAccuracy,
            PlayerAttributeType.DuelHardness => player.DuelHardness,
            PlayerAttributeType.DuelEfficiency => player.DuelEfficiency,
            PlayerAttributeType.CrossingAccuracy => player.CrossingAccuracy,
            PlayerAttributeType.HeaderStrength => player.HeaderStrength,
            PlayerAttributeType.Jumping => player.Jumping,
            PlayerAttributeType.Dribbling => player.Dribbling,
            PlayerAttributeType.LongShotAccuracy => player.LongShotAccuracy,
            PlayerAttributeType.PenaltyKick => player.PenaltyKick,
            PlayerAttributeType.FreeKick => player.FreeKick,
            PlayerAttributeType.Finishing => player.Finishing,
            PlayerAttributeType.Positioning => player.Positioning,
            PlayerAttributeType.GkReflexes => player.GkReflexes,
            PlayerAttributeType.GkHandling => player.GkHandling,
            PlayerAttributeType.GkOneOnOne => player.GkOneOnOne,
            PlayerAttributeType.GkDistribution => player.GkDistribution,
            PlayerAttributeType.GkAerialControl => player.GkAerialControl,
            _ => 0,
        };
    }
}
