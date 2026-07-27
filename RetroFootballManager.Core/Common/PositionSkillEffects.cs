using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // How much a player's strength drops when played out of position: full strength
    // at his main position, a proficiency-based penalty at a secondary position,
    // and a heavy penalty at a completely unfamiliar position.
    public static class PositionSkillEffects
    {
        // Even a perfectly (99) mastered secondary position never quite reaches
        // the strength of the main position.
        private const double BestSecondaryMultiplier = 0.95;
        private const double WorstSecondaryMultiplier = 0.6;

        // Penalty for a position not listed at all for the player.
        private const double OutOfPositionMultiplier = 0.35;

        public static double GetMultiplier(Player player) =>
            GetMultiplier(player, player.EffectivePosition);

        // Strength multiplier if the player were fielded at the given position - used by
        // the lineup selector to rate position fit before actually assigning a player.
        public static double GetMultiplier(Player player, Position candidate)
        {
            if (candidate == player.Position)
                return 1.0;

            var secondary = player.SecondaryPositions.FirstOrDefault(s => s.Position == candidate);
            if (secondary is null)
                return OutOfPositionMultiplier;

            double proficiency = Math.Clamp(secondary.Proficiency, 0, 99) / 99.0;
            return WorstSecondaryMultiplier + ((BestSecondaryMultiplier - WorstSecondaryMultiplier) * proficiency);
        }
    }
}
