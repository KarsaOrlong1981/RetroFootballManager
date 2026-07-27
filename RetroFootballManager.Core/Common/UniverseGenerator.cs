using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Generates the whole game world at season start: 4 leagues of 18 teams (72) with
    // invented German club names. The target average rating drops per league tier
    // (tier 1 strong ... tier 4 weak). Youth players and transfer market pool
    // follow in later milestones.
    public static class UniverseGenerator
    {
        public const int LeagueCount = 4;
        public const int TeamsPerLeague = 18;
        public const int YouthPerTeam = 20;

        // Target average rating per league tier (index 0 = tier 1).
        private static readonly double[] TierTargetRating = [77, 67, 57, 47];

        // Approximate stadium capacity per tier.
        private static readonly int[] TierCapacity = [45000, 24000, 12000, 6000];

        // Starting capital per tier.
        private static readonly int[] TierBalance = [40_000_000, 12_000_000, 3_000_000, 800_000];

        public static (List<League> Leagues, List<Team> Teams) CreateUniverse(
            int season,
            Random? random = null,
            DateTime? seasonStart = null)
        {
            var rng = random ?? Random.Shared;
            var refDate = seasonStart ?? new DateTime(2026, 8, 1);

            // Fixed club roster (see ClubNameGenerator.FixedRoster): the same 72 clubs, same
            // tier assignment, every new game - only player generation still uses rng.
            var clubsByTier = ClubNameGenerator.FixedRoster
                .GroupBy(c => c.Tier)
                .ToDictionary(g => g.Key, g => g.ToList());

            var leagues = new List<League>();
            var teams = new List<Team>();

            for (int tier = 1; tier <= LeagueCount; tier++)
            {
                leagues.Add(new League
                {
                    Name = $"Liga {tier}",
                    Tier = tier,
                    Season = season,
                });

                double targetRating = TierTargetRating[tier - 1];
                var clubs = clubsByTier[tier];

                for (int i = 0; i < TeamsPerLeague; i++)
                {
                    var (name, shortName, _, logoFile) = clubs[i];
                    teams.Add(CreateTeam(name, shortName, logoFile, tier, targetRating, season, rng, refDate));
                }
            }

            return (leagues, teams);
        }

        private static Team CreateTeam(
            string name,
            string shortName,
            string? logoFile,
            int tier,
            double targetRating,
            int season,
            Random rng,
            DateTime refDate)
        {
            // Some spread around the tier target so leagues aren't uniform.
            double teamTarget = targetRating + (rng.NextDouble() * 8 - 4);

            var players = PlayerGenerator.GenerateSquad(
                Nationality.Germany,
                teamTarget,
                squadSize: PlayerGenerator.DefaultPositionPlanSize,
                foreignPlayerChance: 0.15,
                random: rng,
                referenceDate: refDate);

            var team = new Team
            {
                Name = name,
                ShortName = shortName,
                Nationality = Nationality.Germany,
                LeagueTier = tier,
                LogoPath = logoFile, // null = no crest set yet, UI shows an abbreviation placeholder.
                FormationName = "4-4-2",
                // Random playing style per team for variety across the league; orientation
                // starts balanced (the manager/AI adjusts it reactively during matches).
                PlayingStyle = RandomPlayingStyle(rng),
                TacticalOrientation = TacticalOrientation.Balanced,
                TacklingIntensity = TacklingIntensity.Normal,
                Players = players,
                YouthPlayers = YouthGenerator.GenerateYouthSquad(
                    tier, YouthPerTeam, Nationality.Germany, refDate, rng),
                Employees = [StaffGenerator.GenerateStaff(
                    EmployeeType.AssistantCoach, targetRating - 5, Nationality.Germany, rng)],
                Statistics = new TeamStats { Season = season },
                Stadium = BuildStadium(tier, shortName, rng),
                Finances = new Finances
                {
                    CurrentBalance = TierBalance[tier - 1],
                    SeasonBudget = TierBalance[tier - 1] / 2,
                    TransferBudget = TierBalance[tier - 1] / 4,
                    WageBudget = TierBalance[tier - 1] / 3,
                    FinancialHealth = rng.Next(55, 85),
                },
            };

            // Position-correct starting XI + up to 9 on the bench (rest are reserves).
            LineupSelector.SelectLineup(team);
            return team;
        }

        // Stadium maintenance (annual amount, see FinanceService.ApplyMonthlySettlementAsync for
        // the monthly settlement of it) scales with actual capacity and facility levels instead
        // of just the nominal league tier base - so upgrades noticeably affect monthly upkeep.
        private const double MaintenancePerCapacityUnit = 8.0;
        private const double MaintenancePerFacilityLevel = 5_000.0;
        private const double RoofAnnualMaintenance = 20_000.0;

        private static Stadium BuildStadium(int tier, string shortName, Random rng)
        {
            int seating = (int)(TierCapacity[tier - 1] * 0.8);
            int standing = (int)(TierCapacity[tier - 1] * 0.19);
            int loge = TierCapacity[tier - 1] / 100;
            int comfort = tier <= 2 ? 3 : 2;
            int catering = tier <= 2 ? 3 : 2;
            int merchandise = tier <= 2 ? 3 : 2;
            int infrastructure = tier <= 2 ? 3 : 2;
            bool hasRoof = tier == 1 && rng.NextDouble() < 0.5;

            double maintenanceCosts = (seating + standing + loge) * MaintenancePerCapacityUnit
                + (comfort + catering + merchandise + infrastructure) * MaintenancePerFacilityLevel
                + (hasRoof ? RoofAnnualMaintenance : 0);

            return new Stadium
            {
                Name = $"{shortName}-Arena",
                SeatingCapacity = seating,
                StandingCapacity = standing,
                LogeCapacity = loge,
                Condition = rng.Next(55, 90),
                Atmosphere = rng.Next(50, 90),
                // Tier 4 (lowest league): seat 10 EUR, standing 5 EUR, loge 20 EUR - each better
                // league adds +8 EUR to the seat base price, standing/loge stay at a fixed
                // ratio to it (half/double).
                TicketPrice = 10 + (LeagueCount - tier) * 8,
                SeatPrice = 10 + (LeagueCount - tier) * 8,
                StandingPrice = (10 + (LeagueCount - tier) * 8) * 0.5,
                LogePrice = (10 + (LeagueCount - tier) * 8) * 2,
                ComfortLevel = comfort,
                CateringLevel = catering,
                MerchandiseLevel = merchandise,
                InfrastructureLevel = infrastructure,
                HasRoof = hasRoof,
                MaintenanceCosts = maintenanceCosts,
                HomeAdvantage = rng.Next(40, 70),
                WeatherResistance = rng.Next(40, 80),
            };
        }

        private static PlayingStyle RandomPlayingStyle(Random rng)
        {
            var values = Enum.GetValues<PlayingStyle>();
            return values[rng.Next(values.Length)];
        }
    }
}
