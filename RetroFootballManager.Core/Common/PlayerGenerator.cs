using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Generates a full squad with plausible, nationality-matching names and
    // attributes that scatter around a desired team target rating (position,
    // player-to-player variance and attribute-to-attribute variance affect the result).
    public static class PlayerGenerator
    {
        // Ratio of the default 27-player squad; scaled for a different squad size.
        private static readonly (Position Position, int Count)[] DefaultPositionPlan =
        [
            (Position.Goalkeeper, 3),
            (Position.CentralDefender, 4),
            (Position.LeftDefender, 2),
            (Position.RightDefender, 2),
            (Position.LeftWingBack, 1),
            (Position.RightWingBack, 1),
            (Position.DefensiveMidfielder, 2),
            (Position.CentralMidfielder, 2),
            (Position.LeftMidfielder, 2),
            (Position.RightMidfielder, 2),
            (Position.CentralOffenseMidfielder, 1),
            (Position.LeftOffenseMidfielder, 1),
            (Position.RightOffenseMidfielder, 1),
            (Position.Forward, 3),
        ];

        // Squad size that exactly matches DefaultPositionPlan (no scaling/rounding).
        public static readonly int DefaultPositionPlanSize = DefaultPositionPlan.Sum(p => p.Count);

        public static List<Player> GenerateSquad(
            Nationality nationality,
            double targetAverageRating,
            int squadSize = 25,
            double foreignPlayerChance = 0.15,
            double playerVariance = 12,
            double attributeVariance = 10,
            Random? random = null,
            DateTime? referenceDate = null)
        {
            var rng = random ?? Random.Shared;
            var positions = BuildPositionPlan(squadSize);

            return positions
                .Select(position => GeneratePlayer(
                    nationality, position, targetAverageRating, foreignPlayerChance,
                    playerVariance, attributeVariance, rng, referenceDate))
                .ToList();
        }

        public static Player GeneratePlayer(
            Nationality nationality,
            Position position,
            double targetAverageRating,
            double foreignPlayerChance = 0.15,
            double playerVariance = 12,
            double attributeVariance = 10,
            Random? random = null,
            DateTime? referenceDate = null)
        {
            var rng = random ?? Random.Shared;

            var nameNationality = rng.NextDouble() < foreignPlayerChance
                ? RandomOtherNationality(nationality, rng)
                : nationality;
            var (firstName, lastName) = NameBank.GetRandomName(nameNationality, rng);

            int age = rng.Next(17, 35);

            // Each player scatters around the team target rating - not everyone is equally strong.
            double playerBaseQuality = targetAverageRating + NextGaussianish(rng, playerVariance);

            var weights = GetPositionWeights(position);
            int offensive = RollAttribute(playerBaseQuality, weights.Offense, attributeVariance, rng);
            int defensive = RollAttribute(playerBaseQuality, weights.Defense, attributeVariance, rng);
            int gameIntelligence = RollAttribute(playerBaseQuality, weights.Intelligence, attributeVariance, rng);
            int pressingIntensity = RollAttribute(playerBaseQuality, weights.Pressing, attributeVariance, rng);
            int counterSpeed = RollAttribute(playerBaseQuality, weights.Counter, attributeVariance, rng);
            int passingAccuracy = RollAttribute(playerBaseQuality, weights.Passing, attributeVariance, rng);
            int duelHardness = RollAttribute(playerBaseQuality, weights.Hardness, attributeVariance, rng);
            int duelEfficiency = RollAttribute(playerBaseQuality, weights.Efficiency, attributeVariance, rng);
            int crossingAccuracy = RollAttribute(playerBaseQuality, weights.Crossing, attributeVariance, rng);

            // Outfield-only attributes (aerial ability, ball-carrying, set pieces) - 0 for
            // goalkeepers, mirroring how GK-specific attributes are 0 for outfield players below.
            int headerStrength = 0, jumping = 0, dribbling = 0, longShotAccuracy = 0, penaltyKick = 0, freeKick = 0;
            int finishing = 0, positioning = 0;
            if (position != Position.Goalkeeper)
            {
                headerStrength = RollAttribute(playerBaseQuality, weights.Header, attributeVariance, rng);
                jumping = RollAttribute(playerBaseQuality, weights.Jump, attributeVariance, rng);
                dribbling = RollAttribute(playerBaseQuality, weights.Dribbling, attributeVariance, rng);
                longShotAccuracy = RollAttribute(playerBaseQuality, weights.LongShot, attributeVariance, rng);
                penaltyKick = RollAttribute(playerBaseQuality, weights.PenaltyKick, attributeVariance, rng);
                freeKick = RollAttribute(playerBaseQuality, weights.FreeKick, attributeVariance, rng);
                finishing = RollAttribute(playerBaseQuality, weights.Finishing, attributeVariance, rng);
                positioning = RollAttribute(playerBaseQuality, weights.Positioning, attributeVariance, rng);
            }

            // Goalkeeper-specific attributes only make sense for the Goalkeeper position -
            // outfield players keep them at 0 (never read for outfield calculations).
            int gkReflexes = 0, gkHandling = 0, gkOneOnOne = 0, gkDistribution = 0, gkAerialControl = 0;
            if (position == Position.Goalkeeper)
            {
                gkReflexes = RollAttribute(playerBaseQuality, 1.5, attributeVariance, rng);
                gkHandling = RollAttribute(playerBaseQuality, 1.3, attributeVariance, rng);
                gkOneOnOne = RollAttribute(playerBaseQuality, 1.3, attributeVariance, rng);
                gkDistribution = RollAttribute(playerBaseQuality, 1.0, attributeVariance, rng);
                gkAerialControl = RollAttribute(playerBaseQuality, 1.2, attributeVariance, rng);
            }

            double rating = position == Position.Goalkeeper
                ? new[]
                {
                    defensive, duelEfficiency, gameIntelligence, passingAccuracy,
                    gkReflexes, gkHandling, gkOneOnOne, gkDistribution, gkAerialControl,
                }.Average()
                : new[]
                {
                    offensive, defensive, gameIntelligence, pressingIntensity,
                    counterSpeed, passingAccuracy, duelHardness, duelEfficiency, crossingAccuracy,
                    headerStrength, jumping, dribbling, longShotAccuracy, penaltyKick, freeKick,
                    finishing, positioning,
                }.Average();

            // Talent/potential: young players have a much higher ceiling above their
            // current rating, older ones barely any. Always >= current rating.
            double potentialBonus = Math.Max(0, 23 - age) * (0.3 + (rng.NextDouble() * 0.7));
            int talent = Math.Clamp((int)Math.Round(rating + potentialBonus), (int)Math.Round(rating), 99);

            // Date of birth from age relative to the reference date (season start), with
            // a random day of the year for some variety.
            var refDate = referenceDate ?? new DateTime(2026, 8, 1);
            var birthYearDate = refDate.AddYears(-age);
            var dateOfBirth = birthYearDate.AddDays(-rng.Next(0, 365));

            var secondaryPositions = GenerateSecondaryPositions(position, rng);

            return new Player
            {
                Name = $"{firstName} {lastName}",
                Age = age,
                DateOfBirth = dateOfBirth,
                Talent = talent,
                Nationality = nameNationality,
                Position = position,
                Rating = Math.Round(rating, 1),
                Moral = rng.Next(45, 70),
                Size = Math.Round(1.68 + (rng.NextDouble() * 0.28), 2),
                Fitness = rng.Next(80, 100),
                BaseFitness = rng.Next(40, 90),
                OffensivePower = offensive,
                DefensivePower = defensive,
                GameIntelligence = gameIntelligence,
                PressingIntensity = pressingIntensity,
                CounterSpeed = counterSpeed,
                PassingAccuracy = passingAccuracy,
                DuelHardness = duelHardness,
                DuelEfficiency = duelEfficiency,
                CrossingAccuracy = crossingAccuracy,
                GkReflexes = gkReflexes,
                GkHandling = gkHandling,
                GkOneOnOne = gkOneOnOne,
                GkDistribution = gkDistribution,
                GkAerialControl = gkAerialControl,
                HeaderStrength = headerStrength,
                Jumping = jumping,
                Dribbling = dribbling,
                LongShotAccuracy = longShotAccuracy,
                PenaltyKick = penaltyKick,
                FreeKick = freeKick,
                Finishing = finishing,
                Positioning = positioning,
                Personality = RandomPersonality(rng),
                InMatchCharacter = RandomInMatchCharacter(rng),
                Status = PlayerStatus.Available,
                SecondaryPositions = secondaryPositions,
            };
        }

        // Legacy safety net: a Goalkeeper persisted before GK-specific attributes existed loads
        // with all five at 0 (new DB columns default to 0, and there is no way to recover the
        // original generation roll). Backfill them here from the keeper's existing Rating so old
        // saves get a sensible keeper instead of a permanently broken one - a no-op once the
        // fields are non-zero, so it's safe to call on every load.
        public static void BackfillGoalkeeperAttributesIfMissing(Player player, Random? random = null)
        {
            if (player.Position != Position.Goalkeeper)
                return;
            if (player.GkReflexes > 0 || player.GkHandling > 0 || player.GkOneOnOne > 0
                || player.GkDistribution > 0 || player.GkAerialControl > 0)
                return;

            var rng = random ?? new Random(player.Id);
            double baseQuality = player.Rating > 0 ? player.Rating : 55;
            player.GkReflexes = RollAttribute(baseQuality, 1.5, 10, rng);
            player.GkHandling = RollAttribute(baseQuality, 1.3, 10, rng);
            player.GkOneOnOne = RollAttribute(baseQuality, 1.3, 10, rng);
            player.GkDistribution = RollAttribute(baseQuality, 1.0, 10, rng);
            player.GkAerialControl = RollAttribute(baseQuality, 1.2, 10, rng);
        }

        // Same legacy safety net as above, for the Finishing/Positioning columns added later -
        // a no-op once both fields are non-zero, safe to call on every load.
        public static void BackfillFinishingAndPositioningIfMissing(Player player, Random? random = null)
        {
            if (player.Position == Position.Goalkeeper)
                return;
            if (player.Finishing > 0 || player.Positioning > 0)
                return;

            var rng = random ?? new Random(player.Id);
            double baseQuality = player.Rating > 0 ? player.Rating : 55;
            var weights = GetPositionWeights(player.Position);
            player.Finishing = RollAttribute(baseQuality, weights.Finishing, 10, rng);
            player.Positioning = RollAttribute(baseQuality, weights.Positioning, 10, rng);
        }

        // Same legacy safety net as above, for the BaseFitness (Grundfitness) column added
        // later - a no-op once the field is non-zero, safe to call on every load.
        public static void BackfillBaseFitnessIfMissing(Player player, Random? random = null)
        {
            if (player.BaseFitness > 0)
                return;

            var rng = random ?? new Random(player.Id);
            player.BaseFitness = rng.Next(40, 90);
        }

        // Assigns versatile players 1-2 secondary positions from related positions, so the
        // squad has cover across the pitch. Not everyone is versatile; goalkeepers never are.
        private static List<PositionSkill> GenerateSecondaryPositions(Position position, Random rng)
        {
            if (position == Position.Goalkeeper)
                return [];

            var related = PositionRelations.GetRelated(position);
            if (related.Count == 0)
                return [];

            // ~60% of outfield players get a secondary position; ~20% get a second one.
            double roll = rng.NextDouble();
            int count = roll < 0.4 ? 0 : roll < 0.8 ? 1 : 2;
            if (count == 0)
                return [];

            var pool = related.ToList();
            var result = new List<PositionSkill>();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int index = rng.Next(pool.Count);
                var pos = pool[index];
                pool.RemoveAt(index);
                // Proficiency 55-90: usable but below the natural position.
                result.Add(new PositionSkill(pos, rng.Next(55, 91)));
            }

            return result;
        }

        private static List<Position> BuildPositionPlan(int squadSize)
        {
            const int defaultSize = 27;
            if (squadSize == defaultSize)
                return DefaultPositionPlan.SelectMany(p => Enumerable.Repeat(p.Position, p.Count)).ToList();

            // Largest-remainder apportionment: keeps every position represented and
            // spreads rounding gaps by fractional remainder, instead of always
            // trimming/padding whichever position happens to be last in the array
            // (that previously wiped out whole groups, e.g. Forward, for squad sizes
            // other than the default 27).
            var raw = DefaultPositionPlan
                .Select(p => p.Count * (squadSize / (double)defaultSize))
                .ToArray();

            var counts = raw.Select(r => Math.Max(1, (int)Math.Floor(r))).ToArray();
            var remainders = Enumerable.Range(0, raw.Length)
                .Select(i => (Index: i, Remainder: raw[i] - Math.Floor(raw[i])))
                .ToList();

            var deficit = squadSize - counts.Sum();
            if (deficit > 0)
            {
                foreach (var (index, _) in remainders.OrderByDescending(r => r.Remainder).Take(deficit))
                    counts[index]++;
            }
            else if (deficit < 0)
            {
                foreach (var (index, _) in remainders.OrderBy(r => r.Remainder))
                {
                    if (deficit == 0)
                        break;
                    if (counts[index] <= 1)
                        continue;
                    counts[index]--;
                    deficit++;
                }
            }

            var plan = new List<Position>();
            for (int i = 0; i < counts.Length; i++)
                plan.AddRange(Enumerable.Repeat(DefaultPositionPlan[i].Position, counts[i]));

            return plan;
        }

        // How strongly the position shifts an attribute up/down relative to base quality.
        // Additive rather than multiplicative, so the shift stays equally strong at any
        // target rating (low or high) and doesn't get stuck at the cap (99).
        private const double PositionOffsetScale = 18;

        private static int RollAttribute(double baseQuality, double positionWeight, double attributeVariance, Random rng)
        {
            double positionOffset = (positionWeight - 1.0) * PositionOffsetScale;
            double value = baseQuality + positionOffset + NextGaussianish(rng, attributeVariance);
            return Math.Clamp((int)Math.Round(value), 1, 99);
        }

        // Approximates a normal distribution (sum of three random numbers), so values
        // near the mean occur more often than at the extremes of a pure uniform distribution.
        private static double NextGaussianish(Random rng, double variance)
        {
            double sum = (rng.NextDouble() + rng.NextDouble() + rng.NextDouble()) / 3.0;
            return (sum - 0.5) * 2 * variance;
        }

        internal readonly record struct PositionWeights(
            double Offense, double Defense, double Intelligence,
            double Pressing, double Counter, double Passing,
            double Hardness, double Efficiency, double Crossing,
            double Header, double Jump, double Dribbling, double LongShot,
            double PenaltyKick, double FreeKick, double Finishing, double Positioning);

        // Also used by PlayerRoleRating to score a player for a specific slot (not just generation).
        internal static PositionWeights GetPositionWeights(Position position) => position switch
        {
            // Header/jump: central defenders and forwards contest crosses/set pieces the most.
            // Penalty/free-kick: forwards and midfielders are the usual takers, defenders rarely.
            // Finishing: strikers by far, wingers/attacking mids some, everyone else little.
            // Positioning: defensive midfielders and central defenders read the game the most.
            Position.Goalkeeper => new PositionWeights(0.3, 1.5, 1.0, 0.4, 0.3, 0.8, 1.1, 1.4, 0.2, 0.3, 0.5, 0.2, 0.1, 0.2, 0.1, 0.1, 0.5),
            Position.CentralDefender => new PositionWeights(0.5, 1.5, 1.1, 1.0, 0.6, 0.9, 1.4, 1.2, 0.3, 1.6, 1.4, 0.4, 0.3, 0.2, 0.3, 0.2, 1.3),
            Position.LeftDefender => new PositionWeights(0.7, 1.3, 1.0, 1.1, 1.0, 1.0, 1.2, 1.1, 0.9, 1.1, 1.0, 0.7, 0.4, 0.2, 0.4, 0.3, 1.0),
            Position.RightDefender => new PositionWeights(0.7, 1.3, 1.0, 1.1, 1.0, 1.0, 1.2, 1.1, 0.9, 1.1, 1.0, 0.7, 0.4, 0.2, 0.4, 0.3, 1.0),
            Position.LeftWingBack => new PositionWeights(1.0, 1.1, 1.0, 1.2, 1.3, 1.1, 1.0, 1.0, 1.4, 0.7, 0.8, 1.1, 0.5, 0.3, 0.6, 0.4, 0.9),
            Position.RightWingBack => new PositionWeights(1.0, 1.1, 1.0, 1.2, 1.3, 1.1, 1.0, 1.0, 1.4, 0.7, 0.8, 1.1, 0.5, 0.3, 0.6, 0.4, 0.9),
            Position.DefensiveMidfielder => new PositionWeights(0.6, 1.3, 1.2, 1.3, 0.8, 1.0, 1.3, 1.1, 0.5, 1.0, 0.9, 0.7, 0.6, 0.5, 0.6, 0.3, 1.6),
            Position.CentralMidfielder => new PositionWeights(0.9, 0.9, 1.3, 1.1, 0.9, 1.3, 1.0, 1.0, 0.8, 0.8, 0.8, 1.2, 1.3, 1.0, 1.1, 0.6, 1.1),
            Position.LeftMidfielder => new PositionWeights(1.0, 0.7, 1.1, 1.0, 1.2, 1.2, 0.8, 0.9, 1.3, 0.6, 0.7, 1.3, 1.1, 0.8, 1.0, 0.7, 0.8),
            Position.RightMidfielder => new PositionWeights(1.0, 0.7, 1.1, 1.0, 1.2, 1.2, 0.8, 0.9, 1.3, 0.6, 0.7, 1.3, 1.1, 0.8, 1.0, 0.7, 0.8),
            Position.CentralOffenseMidfielder => new PositionWeights(1.2, 0.4, 1.4, 0.7, 1.0, 1.3, 0.6, 0.8, 0.9, 0.7, 0.7, 1.4, 1.3, 1.1, 1.3, 1.1, 0.6),
            Position.LeftOffenseMidfielder => new PositionWeights(1.3, 0.5, 1.3, 0.8, 1.2, 1.2, 0.6, 0.8, 1.2, 0.6, 0.6, 1.4, 1.1, 0.9, 1.1, 1.0, 0.5),
            Position.RightOffenseMidfielder => new PositionWeights(1.3, 0.5, 1.3, 0.8, 1.2, 1.2, 0.6, 0.8, 1.2, 0.6, 0.6, 1.4, 1.1, 0.9, 1.1, 1.0, 0.5),
            Position.Forward => new PositionWeights(1.6, 0.3, 0.9, 0.7, 1.3, 0.8, 0.8, 0.8, 0.9, 1.5, 1.4, 1.2, 1.1, 1.3, 1.0, 1.7, 0.6),
            _ => new PositionWeights(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1),
        };

        private static Personality RandomPersonality(Random rng)
        {
            var values = Enum.GetValues<Personality>();
            // Not every player should have a special personality.
            return rng.NextDouble() < 0.4 ? Personality.None : values[rng.Next(values.Length)];
        }

        // Every player gets one of the 15 fixed in-match character types (no "None" option,
        // unlike Personality above) - this is a transient-match-behavior axis, not an
        // optional trait.
        private static InMatchCharacterType RandomInMatchCharacter(Random rng)
        {
            var values = Enum.GetValues<InMatchCharacterType>();
            return values[rng.Next(values.Length)];
        }

        // Legacy safety net for saves created before InMatchCharacter existed - a no-op once
        // the field is set, safe to call on every load (same pattern as the other Backfill*
        // methods above).
        public static void BackfillInMatchCharacterIfMissing(Player player, Random? random = null)
        {
            if (player.InMatchCharacter is not null)
                return;

            var rng = random ?? new Random(player.Id);
            player.InMatchCharacter = RandomInMatchCharacter(rng);
        }

        private static Nationality RandomOtherNationality(Nationality exclude, Random rng)
        {
            var values = Enum.GetValues<Nationality>().Where(n => n != exclude).ToArray();
            return values[rng.Next(values.Length)];
        }
    }
}
