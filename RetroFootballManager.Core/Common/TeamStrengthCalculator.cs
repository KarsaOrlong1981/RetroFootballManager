using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Calculates a team's effective strength for a match, factoring in
    // tactics, morale, player fitness, player personalities and (for home games)
    // the stadium.
    public static class TeamStrengthCalculator
    {
        // Players below this fitness value contribute barely anything.
        private const double MinFitnessFactor = 0.6;

        // Range of morale's influence: 50 morale = neutral, 0 = -20%, 100 = +20%.
        private const double MoraleSwing = 0.2;

        // Maximum bonus from home advantage/atmosphere.
        private const double MaxStadiumBonus = 0.2;

        public static List<Player> GetLineup(Team team)
        {
            var starters = team.Players.Where(p => p.Status == PlayerStatus.InStartingXI).ToList();
            return starters.Count > 0 ? starters : team.Players;
        }

        // The starting goalkeeper, if one is fielded - for one-on-one duels
        // (shots/penalties against the keeper), independent of the general team average.
        public static Player? GetGoalkeeper(Team team) =>
            GetLineup(team).FirstOrDefault(p => p.EffectivePosition == Position.Goalkeeper);

        public static TeamStrengthProfile Calculate(Team team, bool isHome)
        {
            var lineup = GetLineup(team);
            if (lineup.Count == 0)
                return new TeamStrengthProfile(0, 0, 0, 0, 0, 0);

            var tactic = team.Tactic;

            double attack = 0, defense = 0, midfield = 0, pressing = 0, disciplineRisk = 0, overall = 0;

            foreach (var player in lineup)
            {
                var pm = PersonalityEffects.Get(player.Personality);
                double fitnessFactor = FitnessFactor(player.Fitness);
                double duelEffectivenessMultiplier = TacklingIntensityEffects.GetDuelEffectivenessMultiplier(player, team);
                double foulCardRiskMultiplier = TacklingIntensityEffects.GetFoulCardRiskMultiplier(player, team);

                // Transient in-match morale/character - exactly 1.0 (no-op) outside a live
                // match, since InMatchMoral defaults to the neutral 50 (see Player.cs).
                double characterFactor = InMatchCharacterEffects.AttributeFactor(player.InMatchCharacter, player.InMatchMoral);

                // Penalty if the player isn't used in his natural position
                // (AssignedPosition). Full strength in the natural position, graded
                // penalty in listed secondary positions, high penalty outside the list.
                double positionFitMultiplier = PositionSkillEffects.GetMultiplier(player);

                // Which role this player actually plays decides how much he counts toward each
                // bucket - a center-back barely feeds Attack even with high OffensivePower, and a
                // forward barely feeds Defense - so team strength reflects who plays where, not
                // just a flat squad-wide average.
                var role = GetRoleWeights(player.EffectivePosition);

                // Dribbling (ball-carrying threat, mainly midfielders/wingers - 0 for
                // goalkeepers) adds a further attacking dimension alongside pace/crossing.
                attack += (player.OffensivePower * pm.OffensivePower * tactic.OffensivePowerFactor
                         + player.CounterSpeed * pm.CounterSpeed * tactic.CounterSpeedFactor
                         + player.CrossingAccuracy * tactic.CrossingAccuracyFactor
                         + player.Dribbling * 0.9
                         + player.OffensivePower * pm.AerialThreat * 0.2) / 4.1
                         * fitnessFactor * positionFitMultiplier * role.Attack * characterFactor;

                if (player.EffectivePosition == Position.Goalkeeper)
                {
                    // Goalkeeper-specific attributes replace the generic outfield defense/
                    // midfield formula: shot-stopping/handling/one-on-ones/commanding crosses
                    // feed defense, distribution (build-up passing) feeds midfield.
                    defense += (player.GkReflexes * 0.3 + player.GkHandling * 0.25
                              + player.GkOneOnOne * 0.25 + player.GkAerialControl * 0.2)
                              * tactic.DefensivePowerFactor * fitnessFactor * positionFitMultiplier * characterFactor;

                    midfield += player.GkDistribution * tactic.PassingAccuracyFactor
                              * fitnessFactor * positionFitMultiplier * characterFactor;
                }
                else
                {
                    // Harder tackling (TacklingIntensity) wins duels more often when
                    // the player has good DuelEfficiency - with poor DuelEfficiency it
                    // just makes him clumsier (penalty instead of bonus).
                    defense += (player.DefensivePower * pm.DefensivePower * tactic.DefensivePowerFactor
                              + player.DuelHardness * pm.DuelHardness * tactic.DuelHardnessFactor * duelEffectivenessMultiplier) / 2.0
                              * fitnessFactor * positionFitMultiplier * role.Defense * characterFactor;

                    midfield += (player.GameIntelligence * pm.GameIntelligence * tactic.GameIntelligenceFactor
                               + player.PassingAccuracy * pm.PassingAccuracy * tactic.PassingAccuracyFactor) / 2.0
                               * fitnessFactor * positionFitMultiplier * role.Midfield * characterFactor;
                }

                pressing += (player.PressingIntensity * pm.PressingIntensity * tactic.PressingIntensityFactor
                           + player.DuelEfficiency * pm.DuelEfficiency * tactic.DuelEfficiencyFactor) / 2.0
                           * fitnessFactor * positionFitMultiplier * characterFactor;

                // TacklingIntensity factors in here so a team with many players set to "Hard"
                // commits more fouls overall (and thus concedes more free kicks) regardless of
                // pure tactical choice. At "Normal", foulCardRiskMultiplier is exactly 1.0,
                // so it doesn't change default behavior.
                disciplineRisk += player.DuelHardness * pm.FoulChance * foulCardRiskMultiplier / 100.0;

                overall += tactic.CalculatePlayerTacticalStrength(player) * fitnessFactor * positionFitMultiplier * characterFactor;
            }

            int count = lineup.Count;
            attack /= count;
            defense /= count;
            midfield /= count;
            pressing /= count;
            disciplineRisk /= count;
            overall /= count;

            double moraleFactor = MoraleFactor(team);
            double stadiumFactor = isHome ? StadiumFactor(team.Stadium) : 1.0;
            double totalFactor = moraleFactor * stadiumFactor;

            // Manager skills that only make sense on their own strength bucket - Offensive
            // Creation sharpens Attack, Defensive Organization tightens Defense. Overall/
            // Midfield/Pressing are deliberately untouched (mirrors the characterFactor
            // pattern: purely additive, no existing formula restructured).
            double offensiveCreationFactor = ManagerEffects.OffensiveCreationFactor(team.ManagerProfile);
            double defensiveOrganizationFactor = ManagerEffects.DefensiveOrganizationFactor(team.ManagerProfile);

            return new TeamStrengthProfile(
                Overall: overall * totalFactor,
                Attack: attack * totalFactor * offensiveCreationFactor,
                Defense: defense * totalFactor * defensiveOrganizationFactor,
                Midfield: midfield * totalFactor,
                Pressing: pressing * totalFactor,
                DisciplineRisk: disciplineRisk);
        }

        private readonly record struct RoleWeights(double Attack, double Defense, double Midfield);

        // How much a player at this (effective) position counts toward each strength bucket.
        // Goalkeeper is handled by its own dedicated formula above, so it isn't listed here.
        private static RoleWeights GetRoleWeights(Position position) => position switch
        {
            Position.CentralDefender => new RoleWeights(0.5, 1.5, 0.7),
            Position.LeftDefender or Position.RightDefender => new RoleWeights(0.7, 1.3, 0.8),
            Position.LeftWingBack or Position.RightWingBack => new RoleWeights(1.1, 1.0, 0.9),
            Position.DefensiveMidfielder => new RoleWeights(0.6, 1.2, 1.2),
            Position.CentralMidfielder => new RoleWeights(0.9, 0.8, 1.3),
            Position.LeftMidfielder or Position.RightMidfielder => new RoleWeights(1.1, 0.7, 1.1),
            Position.CentralOffenseMidfielder or Position.LeftOffenseMidfielder or Position.RightOffenseMidfielder
                => new RoleWeights(1.3, 0.5, 1.0),
            Position.Forward => new RoleWeights(1.5, 0.4, 0.7),
            _ => new RoleWeights(1.0, 1.0, 1.0),
        };

        public static double FitnessFactor(int fitness) =>
            MinFitnessFactor + (1.0 - MinFitnessFactor) * Math.Clamp(fitness, 0, 100) / 100.0;

        public static double MoraleFactor(Team team)
        {
            int morale = team.Statistics?.Morale ?? 50;
            return 1.0 + MoraleSwing * (morale - 50) / 50.0;
        }

        public static double StadiumFactor(Stadium? stadium)
        {
            if (stadium is null)
                return 1.0;

            double homeAdvantage = Math.Clamp(stadium.HomeAdvantage, 0, 100) / 100.0;
            double atmosphere = Math.Clamp(stadium.Atmosphere, 0, 100) / 100.0;

            return 1.0 + MaxStadiumBonus * (0.7 * homeAdvantage + 0.3 * atmosphere);
        }
    }
}
