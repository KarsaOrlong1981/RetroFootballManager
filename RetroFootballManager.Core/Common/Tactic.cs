using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // A team's tactical profile is two orthogonal choices combined into nine attribute
    // weighting factors:
    //  - PlayingStyle: the attacking pattern (counter, possession, pressing, wing play,
    //    crosses to the striker).
    //  - TacticalOrientation: how defensive/offensive the team plays overall (a dial from
    //    very defensive to very offensive), applied as a multiplier on top of the style's
    //    base factors - analogous to how TacklingIntensity is its own separate axis.
    public class Tactic
    {
        public PlayingStyle Style { get; }
        public TacticalOrientation Orientation { get; }

        // Weighting factors for player attributes
        public double OffensivePowerFactor { get; }
        public double DefensivePowerFactor { get; }
        public double GameIntelligenceFactor { get; }
        public double PressingIntensityFactor { get; }
        public double CounterSpeedFactor { get; }
        public double PassingAccuracyFactor { get; }
        public double DuelHardnessFactor { get; }
        public double DuelEfficiencyFactor { get; }
        public double CrossingAccuracyFactor { get; }

        private const double MinFactor = 0.35;
        private const double MaxFactor = 2.0;

        public Tactic(PlayingStyle style, TacticalOrientation orientation)
        {
            Style = style;
            Orientation = orientation;

            var b = GetBaseFactors(style);
            var o = GetOrientationMultipliers(orientation);

            OffensivePowerFactor = Combine(b.Offense, o.Offense);
            DefensivePowerFactor = Combine(b.Defense, o.Defense);
            GameIntelligenceFactor = Combine(b.Intelligence, o.Intelligence);
            PressingIntensityFactor = Combine(b.Pressing, o.Pressing);
            CounterSpeedFactor = Combine(b.Counter, o.Counter);
            PassingAccuracyFactor = Combine(b.Passing, o.Passing);
            DuelHardnessFactor = Combine(b.Hardness, o.Hardness);
            DuelEfficiencyFactor = Combine(b.Efficiency, o.Efficiency);
            CrossingAccuracyFactor = Combine(b.Crossing, o.Crossing);
        }

        private static double Combine(double baseFactor, double orientationMultiplier) =>
            Math.Clamp(baseFactor * orientationMultiplier, MinFactor, MaxFactor);

        // Calculates a player's tactical strength
        public double CalculatePlayerTacticalStrength(Player p)
        {
            double strength =
                p.OffensivePower * OffensivePowerFactor +
                p.DefensivePower * DefensivePowerFactor +
                p.GameIntelligence * GameIntelligenceFactor +
                p.PressingIntensity * PressingIntensityFactor +
                p.CounterSpeed * CounterSpeedFactor +
                p.PassingAccuracy * PassingAccuracyFactor +
                p.DuelHardness * DuelHardnessFactor +
                p.DuelEfficiency * DuelEfficiencyFactor +
                p.CrossingAccuracy * CrossingAccuracyFactor;

            return strength / 10.0; // normalization
        }

        private readonly record struct FactorSet(
            double Offense, double Defense, double Intelligence, double Pressing,
            double Counter, double Passing, double Hardness, double Efficiency, double Crossing);

        // Every playing style must set ALL nine factors. Unmentioned attributes would
        // otherwise stay at the C# default 0.0 and effectively remove that player attribute
        // entirely from the strength calculation (see the historical pressing bug).
        private static FactorSet GetBaseFactors(PlayingStyle style) => style switch
        {
            PlayingStyle.CounterAttack => new FactorSet(
                Offense: 1.2, Defense: 0.9, Intelligence: 1.1, Pressing: 1.0,
                Counter: 1.5, Passing: 1.0, Hardness: 1.0, Efficiency: 1.0, Crossing: 1.0),

            PlayingStyle.TikiTaka => new FactorSet(
                Offense: 1.1, Defense: 1.0, Intelligence: 1.3, Pressing: 0.8,
                Counter: 1.0, Passing: 1.5, Hardness: 1.0, Efficiency: 1.0, Crossing: 0.8),

            PlayingStyle.Pressing => new FactorSet(
                Offense: 1.0, Defense: 1.1, Intelligence: 1.0, Pressing: 1.5,
                Counter: 1.0, Passing: 0.9, Hardness: 1.2, Efficiency: 1.0, Crossing: 0.7),

            PlayingStyle.WingPlay => new FactorSet(
                Offense: 1.1, Defense: 0.9, Intelligence: 0.9, Pressing: 0.9,
                Counter: 1.2, Passing: 1.0, Hardness: 1.0, Efficiency: 1.0, Crossing: 1.5),

            PlayingStyle.CrossesToStriker => new FactorSet(
                Offense: 1.3, Defense: 0.9, Intelligence: 0.85, Pressing: 0.9,
                Counter: 0.9, Passing: 0.8, Hardness: 1.0, Efficiency: 1.0, Crossing: 1.6),

            _ => new FactorSet(1, 1, 1, 1, 1, 1, 1, 1, 1),
        };

        // Five levels (-2..+2) around "Balanced": shifts offense-/defense-heavy
        // factors in opposite directions, leaves game intelligence/duel efficiency untouched
        // (orientation is an aggressiveness axis, not a quality axis).
        private static FactorSet GetOrientationMultipliers(TacticalOrientation orientation)
        {
            int level = orientation switch
            {
                TacticalOrientation.VeryDefensive => -2,
                TacticalOrientation.Defensive => -1,
                TacticalOrientation.Offensive => 1,
                TacticalOrientation.VeryOffensive => 2,
                _ => 0,
            };

            return new FactorSet(
                Offense: 1.0 + (level * 0.15),
                Defense: 1.0 - (level * 0.15),
                Intelligence: 1.0,
                Pressing: 1.0,
                Counter: 1.0 + (level * 0.05),
                Passing: 1.0 + (level * 0.05),
                Hardness: 1.0 - (level * 0.08),
                Efficiency: 1.0,
                Crossing: 1.0 + (level * 0.08));
        }
    }
}
