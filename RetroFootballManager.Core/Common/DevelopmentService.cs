using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Long-term player development: young players grow (faster with talent and first-team
    // minutes), older players decline, youth prospects mature (accelerated by a mentor) and
    // graduate into the senior squad once they turn 20. Growth/decline is spread across the
    // season in monthly increments (ApplyMonthlyDevelopment) instead of one lump sum - DevelopSquad
    // only handles season-end bookkeeping (aging, youth graduation, SeasonMinutes reset).
    public static class DevelopmentService
    {
        private const int GraduationAge = 20;

        // The GrowthPoints/DeclinePoints/youth-growth formulas below express a full-SEASON total;
        // ApplyMonthlyDevelopment divides by this to spread it across the season (pre-season +
        // matchdays) instead of applying it all at once at season end.
        public const int MonthsPerSeason = 10;

        // Goalkeepers age via their own GK attributes - their outfield-only fields (incl.
        // Jumping) are always 0, see Player.cs - outfield players decline via these three.
        private static readonly TrainableAttribute[] OutfieldPhysical =
        [
            TrainableAttribute.CounterSpeed, TrainableAttribute.Pressing, TrainableAttribute.DuelHardness,
        ];
        private static readonly TrainableAttribute[] GoalkeeperPhysical =
        [
            TrainableAttribute.GkReflexes, TrainableAttribute.GkOneOnOne,
        ];

        // Season-end bookkeeping: aging, youth graduation, SeasonMinutes reset. The actual
        // attribute growth/decline happens monthly during the season - see ApplyMonthlyDevelopment.
        public static void DevelopSquad(Team team, DateTime newDate, Random? random = null)
        {
            foreach (var player in team.Players)
            {
                player.Age = player.AgeOn(newDate);
                player.SeasonMinutes = 0;
            }

            var graduates = new List<Player>();
            foreach (var youth in team.YouthPlayers)
            {
                youth.Age = youth.AgeOn(newDate);
                youth.SeasonMinutes = 0;
                if (youth.Age >= GraduationAge)
                    graduates.Add(youth);
            }

            foreach (var g in graduates)
            {
                g.IsYouthProspect = false;
                g.Status = PlayerStatus.Available;
                team.YouthPlayers.Remove(g);
                team.Players.Add(g);
            }
        }

        // Applies one month's worth of growth/decline to the whole squad - a no-op (returns
        // false) if already run for this (month, year), so it's safe to call once per day from
        // CalendarAdvanceService.AdvanceOneDayAsync for every team. Returns true when it actually
        // applied development, so the caller knows the team needs to be persisted.
        public static bool ApplyMonthlyDevelopment(Team team, DateTime currentDate, Random? random = null)
        {
            if (team.LastDevelopmentMonth == currentDate.Month && team.LastDevelopmentYear == currentDate.Year)
                return false;

            var rng = random ?? Random.Shared;

            foreach (var player in team.Players)
                DevelopSeniorMonthly(player, team, rng);

            foreach (var youth in team.YouthPlayers)
                DevelopYouthMonthly(youth, team, rng);

            team.LastDevelopmentMonth = currentDate.Month;
            team.LastDevelopmentYear = currentDate.Year;
            return true;
        }

        private static void DevelopSeniorMonthly(Player player, Team team, Random rng)
        {
            double growthPerSeason = GrowthPointsPerSeason(player);
            if (player.Position == Position.Goalkeeper)
                growthPerSeason += GoalkeeperCoachBonus(team);
            double declinePerSeason = DeclinePointsPerSeason(player.Age);

            int growth = RollPoints(growthPerSeason / MonthsPerSeason, rng);
            int decline = RollPoints(declinePerSeason / MonthsPerSeason, rng);

            if (growth > 0) ApplyGrowth(player, growth, rng);
            if (decline > 0) ApplyDecline(player, decline, rng);

            if (growth > 0 || decline > 0)
                TrainingService.RecalculateRating(player);
        }

        private static void DevelopYouthMonthly(Player youth, Team team, Random rng)
        {
            // Youth grow strongly by talent, plus a mentor bonus and any first-team minutes.
            double growthPerSeason = 2 + (youth.Talent / 30.0) + MentorBonus(youth, team) + MinutesBonus(youth.SeasonMinutes);
            if (youth.Position == Position.Goalkeeper)
                growthPerSeason += GoalkeeperCoachBonus(team);

            int growth = RollPoints(growthPerSeason / MonthsPerSeason, rng);
            if (growth > 0)
            {
                ApplyGrowth(youth, growth, rng);
                TrainingService.RecalculateRating(youth);
            }
        }

        private static double GrowthPointsPerSeason(Player player)
        {
            double baseGrowth = player.Age switch
            {
                <= 21 => 2 + (player.Talent / 30.0),
                <= 25 => player.Talent / 45.0,
                _ => 0,
            };
            return baseGrowth + MinutesBonus(player.SeasonMinutes);
        }

        // First-team minutes give young players a noticeable (but not extreme) boost.
        private static int MinutesBonus(int seasonMinutes)
        {
            if (seasonMinutes >= 1500) return 2;
            if (seasonMinutes >= 500) return 1;
            return 0;
        }

        private static int MentorBonus(Player youth, Team team)
        {
            if (youth.MentorId is null)
                return 0;

            var mentor = team.Players.FirstOrDefault(p => p.Id == youth.MentorId);
            if (mentor is null)
                return 0;

            // A strong, experienced mentor accelerates development.
            return mentor.Rating >= 75 ? 2 : mentor.Rating >= 60 ? 1 : 0;
        }

        // A strong goalkeeper coach speeds up development for keepers specifically - mirrors
        // MentorBonus's tiered shape but keyed on the team's best GoalkeeperCoach employee.
        private static int GoalkeeperCoachBonus(Team team)
        {
            var coach = team.Employees
                .Where(e => e.EmployeeType == EmployeeType.GoalkeeperCoach)
                .OrderByDescending(e => e.GoalkeeperTraining)
                .FirstOrDefault();
            if (coach is null)
                return 0;

            return coach.GoalkeeperTraining >= 75 ? 2 : coach.GoalkeeperTraining >= 60 ? 1 : 0;
        }

        // Season-total decline expressed as an expected value (was "2 + 50% chance of +1" etc. -
        // same expected value, but RollPoints now handles the fractional rounding once the total
        // is divided down to a monthly amount).
        private static double DeclinePointsPerSeason(int age) => age switch
        {
            >= 33 => 2.5,
            >= 30 => 1.4,
            _ => 0,
        };

        // Floor + probabilistic round-up on the remainder - same pattern TrainingService.Train
        // uses to turn a fractional "potential" into a whole number of points without losing the
        // fractional part's long-run effect.
        private static int RollPoints(double expectedValue, Random rng)
        {
            if (expectedValue <= 0)
                return 0;

            int points = (int)Math.Floor(expectedValue);
            double remainder = expectedValue - points;
            if (rng.NextDouble() < remainder)
                points++;
            return points;
        }

        private static void ApplyGrowth(Player player, int points, Random rng)
        {
            var pool = TrainingService.ApplicableAttributes(player.Position);
            int cap = Math.Min(99, player.Talent + 8);
            for (int i = 0; i < points; i++)
            {
                var attr = pool[rng.Next(pool.Count)];
                int current = TrainingService.Get(player, attr);
                if (current < cap)
                    TrainingService.Set(player, attr, current + 1);
            }
        }

        private static void ApplyDecline(Player player, int points, Random rng)
        {
            var pool = player.Position == Position.Goalkeeper ? GoalkeeperPhysical : OutfieldPhysical;
            for (int i = 0; i < points; i++)
            {
                var attr = pool[rng.Next(pool.Length)];
                int current = TrainingService.Get(player, attr);
                if (current > 20)
                    TrainingService.Set(player, attr, current - 1);
            }
        }
    }
}
