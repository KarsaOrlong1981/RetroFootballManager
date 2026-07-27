using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // How good a specific player actually is AT a specific slot - a position-weighted composite
    // of his own attributes, not the flat generic Rating average. Used by LineupSelector so the
    // starting XI/bench/formation choice rewards attribute-to-role fit (e.g. a real center-back's
    // Defense/DuelHardness) instead of ranking everyone by the same all-attributes average.
    public static class PlayerRoleRating
    {
        public static double For(Player player, Position slotPosition)
        {
            double baseScore = slotPosition == Position.Goalkeeper
                ? GoalkeeperScore(player)
                : OutfieldScore(player, slotPosition);
            return baseScore * PositionSkillEffects.GetMultiplier(player, slotPosition);
        }

        private static double GoalkeeperScore(Player player) => new[]
        {
            player.DefensivePower, player.DuelEfficiency, player.GameIntelligence, player.PassingAccuracy,
            player.GkReflexes, player.GkHandling, player.GkOneOnOne, player.GkDistribution, player.GkAerialControl,
        }.Average();

        private static double OutfieldScore(Player player, Position slotPosition)
        {
            var w = PlayerGenerator.GetPositionWeights(slotPosition);

            double weightedSum =
                player.OffensivePower * w.Offense + player.DefensivePower * w.Defense +
                player.GameIntelligence * w.Intelligence + player.PressingIntensity * w.Pressing +
                player.CounterSpeed * w.Counter + player.PassingAccuracy * w.Passing +
                player.DuelHardness * w.Hardness + player.DuelEfficiency * w.Efficiency +
                player.CrossingAccuracy * w.Crossing + player.HeaderStrength * w.Header +
                player.Jumping * w.Jump + player.Dribbling * w.Dribbling +
                player.LongShotAccuracy * w.LongShot + player.PenaltyKick * w.PenaltyKick +
                player.FreeKick * w.FreeKick + player.Finishing * w.Finishing +
                player.Positioning * w.Positioning;

            double weightSum =
                w.Offense + w.Defense + w.Intelligence + w.Pressing + w.Counter + w.Passing +
                w.Hardness + w.Efficiency + w.Crossing + w.Header + w.Jump + w.Dribbling +
                w.LongShot + w.PenaltyKick + w.FreeKick + w.Finishing + w.Positioning;

            return weightedSum / weightSum;
        }
    }
}
