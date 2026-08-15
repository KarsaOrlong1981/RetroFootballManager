using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Multipliers by which a player's transient InMatchCharacter shapes his in-match morale
    // reactions and, indirectly, his effective strength this match. Mirrors
    // PersonalityEffects.PersonalityModifier's pattern: an explicit neutral fallback (all
    // factors = 1.0 / 0.0) for null/unknown, since the record struct's implicit
    // zero-initializer would otherwise silently zero out every multiplier.
    public readonly record struct InMatchCharacterModifier(
        double MoraleVolatility = 1.0,          // how strongly InMatchMoral reacts to any event
        double BehindResilience = 1.0,          // >1 = loses less morale while behind
        double LeadComplacency = 1.0,           // >1 = loses more morale/effort while comfortably ahead
        double GoalReactionFactor = 1.0,        // how much morale rises when his side scores
        double ConcededReactionFactor = 1.0,    // how much morale drops when his side concedes
        double CriticismSensitivity = 1.0,      // how strongly a Criticize team talk affects him
        double LowMoraleFoulRisk = 1.0,         // extra foul-risk multiplier once InMatchMoral < 40
        double CrowdSensitivity = 1.0,          // home-atmosphere sensitivity
        double SlowStartPenalty = 0.0);         // one-time InMatchMoral deduction applied at kickoff

    public static class InMatchCharacterEffects
    {
        public static InMatchCharacterModifier Get(InMatchCharacterType? type) => type switch
        {
            InMatchCharacterType.Fighter => new InMatchCharacterModifier(
                BehindResilience: 1.6, ConcededReactionFactor: 0.6),

            InMatchCharacterType.ClutchPerformer => new InMatchCharacterModifier(
                MoraleVolatility: 0.7, BehindResilience: 1.3),

            InMatchCharacterType.Leader => new InMatchCharacterModifier(
                MoraleVolatility: 0.6),

            InMatchCharacterType.MomentumHunter => new InMatchCharacterModifier(
                GoalReactionFactor: 1.6),

            InMatchCharacterType.IceCold => new InMatchCharacterModifier(
                MoraleVolatility: 0.3),

            InMatchCharacterType.NervousUnderPressure => new InMatchCharacterModifier(
                BehindResilience: 0.6, ConcededReactionFactor: 1.4),

            InMatchCharacterType.Complacent => new InMatchCharacterModifier(
                LeadComplacency: 1.6),

            InMatchCharacterType.Hothead => new InMatchCharacterModifier(
                MoraleVolatility: 1.3, LowMoraleFoulRisk: 1.8),

            InMatchCharacterType.FragileConfidence => new InMatchCharacterModifier(
                CriticismSensitivity: 1.7, ConcededReactionFactor: 1.3),

            InMatchCharacterType.LazyWhenLeading => new InMatchCharacterModifier(
                MoraleVolatility: 0.8, LeadComplacency: 1.7),

            InMatchCharacterType.Emotional => new InMatchCharacterModifier(
                MoraleVolatility: 1.6),

            InMatchCharacterType.RiskTaker => new InMatchCharacterModifier(
                LowMoraleFoulRisk: 1.3),

            InMatchCharacterType.CrowdDriven => new InMatchCharacterModifier(
                CrowdSensitivity: 1.6),

            InMatchCharacterType.SlowStarter => new InMatchCharacterModifier(
                SlowStartPenalty: -8),

            InMatchCharacterType.MomentumSensitive => new InMatchCharacterModifier(
                MoraleVolatility: 1.2, GoalReactionFactor: 1.4, ConcededReactionFactor: 1.4),

            // Explicit neutral defaults - see the class doc comment on why this can't just be
            // "new InMatchCharacterModifier()".
            _ => new InMatchCharacterModifier(
                MoraleVolatility: 1.0, BehindResilience: 1.0, LeadComplacency: 1.0,
                GoalReactionFactor: 1.0, ConcededReactionFactor: 1.0, CriticismSensitivity: 1.0,
                LowMoraleFoulRisk: 1.0, CrowdSensitivity: 1.0, SlowStartPenalty: 0.0),
        };

        // Small additive multiplier feeding into TeamStrengthCalculator's Attack/Defense/
        // Midfield/Pressing/Overall formulas alongside the existing tactic/personality
        // factors - a moral swing away from the neutral 50 baseline nudges effective strength
        // by up to +/-15%, scaled by the character's MoraleVolatility. Purely additive: none
        // of TeamStrengthCalculator's existing formulas are restructured, this is just one
        // more multiplier in the chain.
        public static double AttributeFactor(InMatchCharacterType? type, int inMatchMoral)
        {
            double moraleDelta = (Math.Clamp(inMatchMoral, 0, 100) - 50) / 50.0; // -1.0 .. +1.0
            return 1.0 + (moraleDelta * 0.15 * Get(type).MoraleVolatility);
        }
    }
}
