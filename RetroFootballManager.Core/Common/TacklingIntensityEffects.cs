using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // How hard a team/player tackles tactically depends on the acting player's
    // individual DuelEfficiency: a technically strong player wins the ball more
    // often through hard tackling (duel bonus), while a weak player just gets
    // clumsier and picks up more cards. At "Normal" both are exactly neutral
    // (aggressionLevel=0), so default behavior is unchanged.
    public static class TacklingIntensityEffects
    {
        // Centered on 0 at "Normal": negative values = more cautious than normal,
        // positive values = more aggressive than normal.
        private static double AggressionLevel(TacklingIntensity intensity) => intensity switch
        {
            TacklingIntensity.Cautious => -0.5,
            TacklingIntensity.Normal => 0.0,
            TacklingIntensity.Moderate => 0.5,
            TacklingIntensity.Hard => 1.0,
            _ => 0.0,
        };

        // A player uses the team setting unless individually overridden
        // (e.g. via a live instruction during the match).
        public static TacklingIntensity GetEffective(Player player, Team team) =>
            player.TacklingIntensity ?? team.TacklingIntensity;

        // 0..~1: how cleanly/successfully the player normally wins his duels.
        private static double SkillFactor(Player player) => Math.Clamp(player.DuelEfficiency, 0, 99) / 100.0;

        // Bonus/penalty to duel strength (feeds into Defense/Pressing).
        public static double GetDuelEffectivenessMultiplier(Player player, Team team)
        {
            double aggression = AggressionLevel(GetEffective(player, team));
            double skill = SkillFactor(player);

            if (aggression >= 0)
            {
                // Moderate/Hard: more effort pays off with good DuelEfficiency
                // (bonus), but backfires with poor DuelEfficiency (penalty).
                return 1.0 + (aggression * (skill - 0.5));
            }

            // Cautious: playing cautiously fundamentally wins the ball less often -
            // this is always a penalty, never a bonus. Good DuelEfficiency softens it
            // considerably but never fully cancels it: even a very good duelist wins
            // fewer balls playing cautiously than with Hard.
            double caution = -aggression;
            return 1.0 - (caution * (1.0 - skill));
        }

        // How much a foul by this player escalates into a card. Always 1.0 at
        // "Normal" (neutral, skill-independent). At Moderate/Hard, individual duel
        // efficiency shows through: a poor value plus hard tackling makes cards
        // practically inevitable, a good value stays cleaner than the tactic
        // baseline. The reverse applies at Cautious.
        public static double GetFoulCardRiskMultiplier(Player player, Team team)
        {
            double aggression = AggressionLevel(GetEffective(player, team));
            double skill = SkillFactor(player);
            double baseRisk = 1.0 + aggression;
            double clumsinessAdjustment = 1.0 + (aggression * (0.5 - skill) * 1.5);
            return baseRisk * clumsinessAdjustment;
        }

        // Team average over the starting XI - for decisions where the specific
        // fouling player isn't known yet (e.g. "does a penalty foul happen at all?").
        public static double GetTeamAverageFoulCardRiskMultiplier(Team team)
        {
            var lineup = TeamStrengthCalculator.GetLineup(team);
            return lineup.Count == 0 ? 1.0 : lineup.Average(p => GetFoulCardRiskMultiplier(p, team));
        }
    }
}
