using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Seasonal player development: players age, young players grow (faster with talent and
    // first-team minutes), older players decline, youth prospects mature (accelerated by a
    // mentor) and graduate into the senior squad once they turn 20.
    public static class DevelopmentService
    {
        private const int GraduationAge = 20;

        // Physical attributes that fade with age.
        private static readonly TrainableAttribute[] Physical =
        [
            TrainableAttribute.CounterSpeed, TrainableAttribute.Pressing, TrainableAttribute.DuelHardness,
        ];

        private static readonly TrainableAttribute[] AllAttributes = Enum.GetValues<TrainableAttribute>();

        public static void DevelopSquad(Team team, DateTime newDate, Random? random = null)
        {
            var rng = random ?? Random.Shared;

            foreach (var player in team.Players)
                DevelopSenior(player, team, newDate, rng);

            // Youth: mature (mentor-accelerated), then graduate the over-age ones.
            var graduates = new List<Player>();
            foreach (var youth in team.YouthPlayers)
            {
                DevelopYouth(youth, team, newDate, rng);
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

        private static void DevelopSenior(Player player, Team team, DateTime newDate, Random rng)
        {
            player.Age = player.AgeOn(newDate);

            int growth = GrowthPoints(player, rng);
            if (player.Position == Position.Goalkeeper)
                growth += GoalkeeperCoachBonus(team);
            int decline = DeclinePoints(player.Age, rng);

            if (growth > 0) ApplyGrowth(player, growth, rng);
            if (decline > 0) ApplyDecline(player, decline, rng);

            player.SeasonMinutes = 0;
            TrainingService.RecalculateRating(player);
        }

        private static void DevelopYouth(Player youth, Team team, DateTime newDate, Random rng)
        {
            youth.Age = youth.AgeOn(newDate);

            // Youth grow strongly by talent, plus a mentor bonus and any first-team minutes.
            int growth = 2 + youth.Talent / 30 + MentorBonus(youth, team) + MinutesBonus(youth.SeasonMinutes);
            if (youth.Position == Position.Goalkeeper)
                growth += GoalkeeperCoachBonus(team);
            ApplyGrowth(youth, growth, rng);

            youth.SeasonMinutes = 0;
            TrainingService.RecalculateRating(youth);
        }

        private static int GrowthPoints(Player player, Random rng)
        {
            int baseGrowth = player.Age switch
            {
                <= 21 => 2 + player.Talent / 30,
                <= 25 => player.Talent / 45,
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

        private static int DeclinePoints(int age, Random rng) => age switch
        {
            >= 33 => 2 + (rng.NextDouble() < 0.5 ? 1 : 0),
            >= 30 => 1 + (rng.NextDouble() < 0.4 ? 1 : 0),
            _ => 0,
        };

        private static void ApplyGrowth(Player player, int points, Random rng)
        {
            for (int i = 0; i < points; i++)
            {
                var attr = AllAttributes[rng.Next(AllAttributes.Length)];
                int cap = Math.Min(99, player.Talent + 8);
                if (Get(player, attr) < cap)
                    Set(player, attr, Get(player, attr) + 1);
            }
        }

        private static void ApplyDecline(Player player, int points, Random rng)
        {
            for (int i = 0; i < points; i++)
            {
                var attr = Physical[rng.Next(Physical.Length)];
                if (Get(player, attr) > 20)
                    Set(player, attr, Get(player, attr) - 1);
            }
        }

        private static int Get(Player p, TrainableAttribute a) => a switch
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
            TrainableAttribute.Finishing => p.Finishing,
            TrainableAttribute.Positioning => p.Positioning,
            _ => 0,
        };

        private static void Set(Player p, TrainableAttribute a, int value)
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
                case TrainableAttribute.Finishing: p.Finishing = value; break;
                case TrainableAttribute.Positioning: p.Positioning = value; break;
            }
        }
    }
}
