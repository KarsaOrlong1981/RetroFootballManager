using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Multipliers by which a player's personality affects his attributes and
    // in-match behavior (fouls, aerial strength, morale stability).
    public readonly record struct PersonalityModifier(
        double OffensivePower = 1.0,
        double DefensivePower = 1.0,
        double GameIntelligence = 1.0,
        double PressingIntensity = 1.0,
        double CounterSpeed = 1.0,
        double PassingAccuracy = 1.0,
        double DuelHardness = 1.0,
        double DuelEfficiency = 1.0,
        double FoulChance = 1.0,
        double AerialThreat = 1.0,
        double MoraleStability = 1.0);

    public static class PersonalityEffects
    {
        public static PersonalityModifier Get(Personality personality) => personality switch
        {
            Personality.Maestro => new PersonalityModifier(
                GameIntelligence: 1.25, PassingAccuracy: 1.2),

            Personality.Hothead => new PersonalityModifier(
                DuelHardness: 1.3, DuelEfficiency: 0.9, FoulChance: 1.6),

            Personality.Workhorse => new PersonalityModifier(
                PressingIntensity: 1.3, DefensivePower: 1.15),

            Personality.Sprinter => new PersonalityModifier(
                CounterSpeed: 1.3, OffensivePower: 1.1),

            Personality.Strategist => new PersonalityModifier(
                GameIntelligence: 1.2, DefensivePower: 1.1),

            Personality.Leader => new PersonalityModifier(
                MoraleStability: 1.3, GameIntelligence: 1.05, DefensivePower: 1.05),

            Personality.Technician => new PersonalityModifier(
                PassingAccuracy: 1.3, DuelEfficiency: 1.1),

            Personality.Enforcer => new PersonalityModifier(
                DuelHardness: 1.35, FoulChance: 1.3),

            Personality.HeaderBeast => new PersonalityModifier(
                AerialThreat: 1.5, OffensivePower: 1.1),

            // "new PersonalityModifier()" with no arguments would hit the record struct's
            // implicit zero-initializer instead of the constructor defaults (=1.0),
            // so all factors are set to neutral explicitly here.
            _ => new PersonalityModifier(
                OffensivePower: 1.0, DefensivePower: 1.0, GameIntelligence: 1.0,
                PressingIntensity: 1.0, CounterSpeed: 1.0, PassingAccuracy: 1.0,
                DuelHardness: 1.0, DuelEfficiency: 1.0, FoulChance: 1.0,
                AerialThreat: 1.0, MoraleStability: 1.0),
        };
    }
}
