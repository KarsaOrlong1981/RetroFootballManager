using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public static class TrainingService
    {
        private const double WeeklyBaseRate = 0.18;

        // Team-wide training spreads a smaller effect across 1-2 attributes for the whole squad,
        // so it doesn't stack with individual focus training to double someone's growth rate.
        private const double TeamTrainingScale = 0.5;

        public static string Label(TrainableAttribute attribute) => attribute switch
        {
            TrainableAttribute.None => "Kein Fokus",
            TrainableAttribute.Offensive => "Offensivkraft",
            TrainableAttribute.Defensive => "Defensivkraft",
            TrainableAttribute.GameIntelligence => "Spielintelligenz",
            TrainableAttribute.Pressing => "Pressing",
            TrainableAttribute.CounterSpeed => "Kontertempo",
            TrainableAttribute.Passing => "Passgenauigkeit",
            TrainableAttribute.DuelHardness => "Zweikampfhärte",
            TrainableAttribute.DuelEfficiency => "Zweikampfeffizienz",
            TrainableAttribute.Crossing => "Flanken",
            TrainableAttribute.GkReflexes => "Reflexe",
            TrainableAttribute.GkHandling => "Ballsicherheit",
            TrainableAttribute.GkOneOnOne => "Eins-gegen-eins",
            TrainableAttribute.GkDistribution => "Spieleröffnung",
            TrainableAttribute.GkAerialControl => "Herauslaufen/Flanken",
            TrainableAttribute.HeaderStrength => "Kopfballstärke",
            TrainableAttribute.Jumping => "Sprungkraft",
            TrainableAttribute.Dribbling => "Dribbling",
            TrainableAttribute.LongShot => "Weitschuss",
            TrainableAttribute.PenaltyKick => "Elfmeterstärke",
            TrainableAttribute.FreeKick => "Freistoßstärke",
            TrainableAttribute.Finishing => "Abschluss",
            TrainableAttribute.Positioning => "Stellungsspiel",
            _ => attribute.ToString(),
        };

        // Goalkeepers train goalkeeper-specific attributes (plus game reading/passing, still
        // relevant for a modern keeper); outfield players never see/train GK attributes and
        // vice versa. Used by the UI (attribute picker) and by EnsureAiFocusAssigned.
        public static IReadOnlyList<TrainableAttribute> ApplicableAttributes(Position position) =>
            position == Position.Goalkeeper ? GoalkeeperAttributes : OutfieldAttributes;

        private static readonly TrainableAttribute[] GoalkeeperAttributes =
        [
            TrainableAttribute.GkReflexes, TrainableAttribute.GkHandling, TrainableAttribute.GkOneOnOne,
            TrainableAttribute.GkDistribution, TrainableAttribute.GkAerialControl,
            TrainableAttribute.GameIntelligence, TrainableAttribute.Passing,
        ];

        private static readonly TrainableAttribute[] OutfieldAttributes =
        [
            TrainableAttribute.Offensive, TrainableAttribute.Defensive, TrainableAttribute.GameIntelligence,
            TrainableAttribute.Pressing, TrainableAttribute.CounterSpeed, TrainableAttribute.Passing,
            TrainableAttribute.DuelHardness, TrainableAttribute.DuelEfficiency, TrainableAttribute.Crossing,
            TrainableAttribute.HeaderStrength, TrainableAttribute.Jumping, TrainableAttribute.Dribbling,
            TrainableAttribute.LongShot, TrainableAttribute.PenaltyKick, TrainableAttribute.FreeKick,
            TrainableAttribute.Finishing, TrainableAttribute.Positioning,
        ];

        public static string Label(TeamTrainingFocus focus) => focus switch
        {
            TeamTrainingFocus.Pressing => "Pressing",
            TeamTrainingFocus.CrossesToStriker => "Flanken auf Stürmer",
            TeamTrainingFocus.TikiTaka => "Ballbesitz",
            TeamTrainingFocus.CounterAttack => "Kontern",
            TeamTrainingFocus.Offensive => "Offensive",
            TeamTrainingFocus.Defensive => "Defensive",
            TeamTrainingFocus.WingPlay => "Flügelspiel",
            TeamTrainingFocus.Konditionstraining => "Konditionstraining",
            _ => focus.ToString(),
        };

        // Applies one week of training to a team: each player's individual focus (if set)
        // plus the team-wide focus (if set). isHuman teams always train at full (1.0) pace;
        // COM teams are scaled by difficulty and by their own morale/fitness state, so a
        // struggling low-morale AI side doesn't develop as fast as a thriving one.
        public static void ApplyWeeklyTraining(Team team, bool isHuman, Difficulty difficulty, Random? random = null)
        {
            var rng = random ?? Random.Shared;

            if (!isHuman)
                EnsureAiFocusAssigned(team, rng);

            double scale = isHuman ? 1.0 : DifficultyScale(difficulty) * MoraleFitnessScale(team);

            foreach (var player in team.Players)
            {
                if (player.CurrentTrainingFocus is TrainableAttribute focus)
                    Train(player, focus, team, rng, scale);
            }

            // Team-wide tactical focus (Pressing, Tiki-Taka, ...) only makes sense for outfield
            // attributes - goalkeepers keep developing solely through their individual GK focus.
            // Konditionstraining is the exception: it raises BaseFitness for everyone, keepers
            // included, since fitness/stamina isn't a position-specific skill.
            if (team.TeamTrainingFocus is TeamTrainingFocus.Konditionstraining)
            {
                foreach (var player in team.Players)
                    TrainBaseFitness(player, team, rng, scale * TeamTrainingScale);
            }
            else if (team.TeamTrainingFocus is TeamTrainingFocus teamFocus)
            {
                foreach (var attribute in AttributesFor(teamFocus))
                    foreach (var player in team.Players.Where(p => p.Position != Position.Goalkeeper))
                        Train(player, attribute, team, rng, scale * TeamTrainingScale);
            }

            ManagerGrowthService.ApplyWeeklyTrainingFocusGrowth(team.ManagerProfile, team);
        }

        // Konditionstraining's effect: raises BaseFitness (Grundfitness) over the season, same
        // shape as Train() (age/coach/ceiling/morale factors) but not capped by Talent - stamina
        // isn't a potential-limited skill the way the other attributes are.
        private const int BaseFitnessCap = 99;

        private static void TrainBaseFitness(Player player, Team team, Random rng, double externalScale)
        {
            if (player.BaseFitness >= BaseFitnessCap)
                return;

            double ageFactor = AgeFactor(player.Age);
            double coachFactor = Math.Clamp(BestFitnessCoachRating(team) / 70.0, 0.7, 1.3);
            double ceilingFactor = Math.Clamp((BaseFitnessCap - player.BaseFitness) / 20.0, 0.15, 1.0);
            double moraleFactor = PlayerMoraleFactor(player.Moral);
            double talkMotivationFactor = 1.0 + player.TalkMotivationBoost;
            double managerFactor = ManagerEffects.TrainingDesignFactor(team.ManagerProfile);

            double potential = WeeklyBaseRate * ageFactor * coachFactor * ceilingFactor * moraleFactor * talkMotivationFactor * managerFactor * externalScale;
            int gain = (int)Math.Floor(potential);
            if (rng.NextDouble() < potential - gain)
                gain++;

            gain = Math.Min(gain, BaseFitnessCap - player.BaseFitness);
            if (gain > 0)
                player.BaseFitness += gain;
        }

        private static int BestFitnessCoachRating(Team team)
        {
            var coach = team.Employees
                .Where(e => e.EmployeeType == EmployeeType.FitnessCoach)
                .OrderByDescending(e => e.FitnessTraining)
                .FirstOrDefault();
            return coach?.FitnessTraining ?? 50;
        }

        // Auto-picks a sensible training focus for AI-controlled players/teams that don't
        // have one yet (or whose focus has hit its cap): the player's currently weakest
        // attribute, and a team focus derived from the team's playing style/orientation.
        public static void EnsureAiFocusAssigned(Team team, Random? random = null)
        {
            var rng = random ?? Random.Shared;

            foreach (var player in team.Players)
            {
                if (player.CurrentTrainingFocus is TrainableAttribute focus && IsAtCap(player, focus))
                    player.CurrentTrainingFocus = null;
            }

            foreach (var player in team.Players.Where(p => p.CurrentTrainingFocus is null))
                player.CurrentTrainingFocus = PickWeakestAttribute(player, rng);

            team.TeamTrainingFocus ??= MapStyleToTeamFocus(team.PlayingStyle, team.TacticalOrientation);
        }

        // Runs one week's worth of training for a single attribute and returns the actual
        // gain applied (0 or more). externalScale folds in difficulty/morale/fitness (AI) or
        // the team-training weight; leave at 1.0 for a plain individual-focus week.
        public static int Train(Player player, TrainableAttribute attribute, Team team, Random? random = null, double externalScale = 1.0)
        {
            var rng = random ?? Random.Shared;

            int current = Get(player, attribute);
            int cap = Math.Min(99, player.Talent + 8);
            if (current >= cap)
                return 0;

            double talentFactor = Math.Clamp(player.Talent / 60.0, 0.5, 1.6);
            double ageFactor = AgeFactor(player.Age);
            double coachFactor = CoachFactor(team, attribute, player);
            double ceilingFactor = Math.Clamp((cap - current) / 20.0, 0.15, 1.0);
            double moraleFactor = PlayerMoraleFactor(player.Moral);
            double talkMotivationFactor = 1.0 + player.TalkMotivationBoost;
            double managerFactor = ManagerEffects.TrainingDesignFactor(team.ManagerProfile);

            double potential = WeeklyBaseRate * talentFactor * ageFactor * coachFactor * ceilingFactor * moraleFactor * talkMotivationFactor * managerFactor * externalScale;
            int gain = (int)Math.Floor(potential);
            if (rng.NextDouble() < potential - gain)
                gain++;

            gain = Math.Min(gain, cap - current);
            if (gain > 0)
            {
                Set(player, attribute, current + gain);
                RecalculateRating(player);
            }
            return gain;
        }

        // The co-trainer contribution (0.7..1.3). Uses the best relevant coach on the team;
        // no staff → neutral.
        public static double CoachFactor(Team team, TrainableAttribute attribute, Player player)
        {
            int rating = BestCoachRating(team, attribute, player);
            return Math.Clamp(rating / 70.0, 0.7, 1.3);
        }

        private static int BestCoachRating(Team team, TrainableAttribute attribute, Player player)
        {
            if (team.Employees.Count == 0)
                return 50;

            Func<Employee, int> selector;
            if (player.Position == Position.Goalkeeper)
                selector = e => e.GoalkeeperTraining;
            else
                selector = attribute switch
                {
                    TrainableAttribute.Offensive or TrainableAttribute.CounterSpeed or TrainableAttribute.Crossing
                        => e => e.OffensiveTraining,
                    TrainableAttribute.Defensive or TrainableAttribute.DuelHardness or TrainableAttribute.DuelEfficiency
                        => e => e.DefensiveTraining,
                    TrainableAttribute.Pressing or TrainableAttribute.HeaderStrength or TrainableAttribute.Jumping
                        => e => e.FitnessTraining, // physical/athletic attributes
                    TrainableAttribute.Dribbling or TrainableAttribute.LongShot
                        or TrainableAttribute.PenaltyKick or TrainableAttribute.FreeKick or TrainableAttribute.Finishing
                        => e => e.OffensiveTraining, // technical attacking attributes
                    TrainableAttribute.Positioning => e => e.DefensiveTraining,
                    _ => e => (e.OffensiveTraining + e.DefensiveTraining) / 2, // intelligence/passing: general
                };

            return team.Employees.Max(selector);
        }

        private static double AgeFactor(int age) => age switch
        {
            <= 19 => 1.5,
            <= 21 => 1.3,
            <= 24 => 1.1,
            <= 28 => 0.85,
            <= 31 => 0.55,
            _ => 0.3,
        };

        // A demotivated player barely progresses even with great talent/coaching; a happy one
        // trains noticeably faster. 50 morale = neutral (matches the team-level MoraleFitnessScale
        // convention used for COM teams), 0 = -30%, 100 = +30%.
        private static double PlayerMoraleFactor(int moral) =>
            0.7 + (0.6 * Math.Clamp(moral, 0, 100) / 100.0);

        private static double DifficultyScale(Difficulty difficulty) => difficulty switch
        {
            Difficulty.Easy => 0.7,
            Difficulty.Hard => 1.3,
            _ => 1.0,
        };

        // Ties COM development to team state (form/morale, average fitness) instead of a flat
        // rate, so a struggling/low-morale AI side doesn't simply out-develop everyone.
        private static double MoraleFitnessScale(Team team)
        {
            int morale = team.Statistics?.Morale ?? 50;
            double moraleFactor = 0.7 + (0.6 * morale / 100.0); // 0.7..1.3
            double avgFitness = team.Players.Count == 0 ? 90 : team.Players.Average(p => p.Fitness);
            double fitnessFactor = 0.7 + (0.6 * avgFitness / 100.0); // 0.7..1.3
            return Math.Clamp(moraleFactor * fitnessFactor, 0.4, 1.6);
        }

        private static bool IsAtCap(Player player, TrainableAttribute attribute) =>
            Get(player, attribute) >= Math.Min(99, player.Talent + 8);

        // Improve-your-weakest-stat heuristic, restricted to the attributes that apply to the
        // player's position (goalkeepers never get assigned an outfield focus and vice versa).
        private static TrainableAttribute PickWeakestAttribute(Player player, Random rng)
        {
            var candidates = ApplicableAttributes(player.Position);
            return candidates.OrderBy(a => Get(player, a)).ThenBy(_ => rng.Next()).First();
        }

        private static TeamTrainingFocus MapStyleToTeamFocus(PlayingStyle style, TacticalOrientation orientation)
        {
            var fromStyle = style switch
            {
                PlayingStyle.Pressing => TeamTrainingFocus.Pressing,
                PlayingStyle.CrossesToStriker => TeamTrainingFocus.CrossesToStriker,
                PlayingStyle.TikiTaka => TeamTrainingFocus.TikiTaka,
                PlayingStyle.CounterAttack => TeamTrainingFocus.CounterAttack,
                PlayingStyle.WingPlay => TeamTrainingFocus.WingPlay,
                _ => (TeamTrainingFocus?)null,
            };
            if (fromStyle is TeamTrainingFocus f)
                return f;

            return orientation is TacticalOrientation.Offensive or TacticalOrientation.VeryOffensive
                ? TeamTrainingFocus.Offensive
                : TeamTrainingFocus.Defensive;
        }

        private static TrainableAttribute[] AttributesFor(TeamTrainingFocus focus) => focus switch
        {
            TeamTrainingFocus.Pressing => [TrainableAttribute.Pressing, TrainableAttribute.DuelHardness, TrainableAttribute.DuelEfficiency],
            TeamTrainingFocus.CrossesToStriker => [TrainableAttribute.Crossing, TrainableAttribute.Offensive, TrainableAttribute.HeaderStrength, TrainableAttribute.Jumping],
            TeamTrainingFocus.TikiTaka => [TrainableAttribute.Passing, TrainableAttribute.GameIntelligence, TrainableAttribute.Dribbling],
            TeamTrainingFocus.CounterAttack => [TrainableAttribute.CounterSpeed, TrainableAttribute.Offensive, TrainableAttribute.Passing],
            TeamTrainingFocus.Offensive => [TrainableAttribute.Offensive, TrainableAttribute.Crossing, TrainableAttribute.Passing, TrainableAttribute.FreeKick, TrainableAttribute.PenaltyKick,TrainableAttribute.LongShot],
            TeamTrainingFocus.Defensive => [TrainableAttribute.Defensive, TrainableAttribute.DuelHardness, TrainableAttribute.DuelEfficiency],
            TeamTrainingFocus.WingPlay => [TrainableAttribute.Crossing, TrainableAttribute.CounterSpeed, TrainableAttribute.Offensive, TrainableAttribute.Dribbling],
            _ => [],
        };

        internal static int Get(Player p, TrainableAttribute a) => a switch
        {
            TrainableAttribute.Offensive => p.OffensivePower,
            TrainableAttribute.Defensive => p.DefensivePower,
            TrainableAttribute.GameIntelligence => p.GameIntelligence,
            TrainableAttribute.Pressing => p.PressingIntensity,
            TrainableAttribute.CounterSpeed => p.CounterSpeed,
            TrainableAttribute.Passing => p.PassingAccuracy,
            TrainableAttribute.DuelHardness => p.DuelHardness,
            TrainableAttribute.DuelEfficiency => p.DuelEfficiency,
            TrainableAttribute.Crossing => p.CrossingAccuracy,
            TrainableAttribute.GkReflexes => p.GkReflexes,
            TrainableAttribute.GkHandling => p.GkHandling,
            TrainableAttribute.GkOneOnOne => p.GkOneOnOne,
            TrainableAttribute.GkDistribution => p.GkDistribution,
            TrainableAttribute.GkAerialControl => p.GkAerialControl,
            TrainableAttribute.HeaderStrength => p.HeaderStrength,
            TrainableAttribute.Jumping => p.Jumping,
            TrainableAttribute.Dribbling => p.Dribbling,
            TrainableAttribute.LongShot => p.LongShotAccuracy,
            TrainableAttribute.PenaltyKick => p.PenaltyKick,
            TrainableAttribute.FreeKick => p.FreeKick,
            TrainableAttribute.Finishing => p.Finishing,
            TrainableAttribute.Positioning => p.Positioning,
            _ => 0,
        };

        internal static void Set(Player p, TrainableAttribute a, int value)
        {
            switch (a)
            {
                case TrainableAttribute.Offensive: p.OffensivePower = value; break;
                case TrainableAttribute.Defensive: p.DefensivePower = value; break;
                case TrainableAttribute.GameIntelligence: p.GameIntelligence = value; break;
                case TrainableAttribute.Pressing: p.PressingIntensity = value; break;
                case TrainableAttribute.CounterSpeed: p.CounterSpeed = value; break;
                case TrainableAttribute.Passing: p.PassingAccuracy = value; break;
                case TrainableAttribute.DuelHardness: p.DuelHardness = value; break;
                case TrainableAttribute.DuelEfficiency: p.DuelEfficiency = value; break;
                case TrainableAttribute.Crossing: p.CrossingAccuracy = value; break;
                case TrainableAttribute.GkReflexes: p.GkReflexes = value; break;
                case TrainableAttribute.GkHandling: p.GkHandling = value; break;
                case TrainableAttribute.GkOneOnOne: p.GkOneOnOne = value; break;
                case TrainableAttribute.GkDistribution: p.GkDistribution = value; break;
                case TrainableAttribute.GkAerialControl: p.GkAerialControl = value; break;
                case TrainableAttribute.HeaderStrength: p.HeaderStrength = value; break;
                case TrainableAttribute.Jumping: p.Jumping = value; break;
                case TrainableAttribute.Dribbling: p.Dribbling = value; break;
                case TrainableAttribute.LongShot: p.LongShotAccuracy = value; break;
                case TrainableAttribute.PenaltyKick: p.PenaltyKick = value; break;
                case TrainableAttribute.FreeKick: p.FreeKick = value; break;
                case TrainableAttribute.Finishing: p.Finishing = value; break;
                case TrainableAttribute.Positioning: p.Positioning = value; break;
            }
        }

        // Keeps the overall rating in sync with the position's relevant attributes (same
        // averaging the player generator uses) - goalkeepers rate off GK-specific attributes
        // instead of the outfield ones (Offensive/Pressing/Crossing/... don't apply to them).
        public static void RecalculateRating(Player p)
        {
            double rating = p.Position == Position.Goalkeeper
                ? new[]
                {
                    p.DefensivePower, p.DuelEfficiency, p.GameIntelligence, p.PassingAccuracy,
                    p.GkReflexes, p.GkHandling, p.GkOneOnOne, p.GkDistribution, p.GkAerialControl,
                }.Average()
                : new[]
                {
                    p.OffensivePower, p.DefensivePower, p.GameIntelligence, p.PressingIntensity,
                    p.CounterSpeed, p.PassingAccuracy, p.DuelHardness, p.DuelEfficiency, p.CrossingAccuracy,
                    p.HeaderStrength, p.Jumping, p.Dribbling, p.LongShotAccuracy, p.PenaltyKick, p.FreeKick,
                    p.Finishing, p.Positioning,
                }.Average();
            p.Rating = Math.Round(rating, 1);
        }
    }
}
