using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Generates youth prospects (ages 15-19). Higher-tier academies produce more talented
    // youngsters. Prospects have a low current rating but a high potential (Talent) so they
    // can develop strongly through training, mentoring and first-team minutes.
    public static class YouthGenerator
    {
        private static readonly Position[] Positions = Enum.GetValues<Position>();

        public static List<Player> GenerateYouthSquad(
            int tier,
            int count,
            Nationality nationality,
            DateTime referenceDate,
            Random? random = null)
        {
            var rng = random ?? Random.Shared;
            var youth = new List<Player>(count);
            for (int i = 0; i < count; i++)
                youth.Add(GenerateYouth(tier, nationality, referenceDate, rng));
            return youth;
        }

        public static Player GenerateYouth(int tier, Nationality nationality, DateTime referenceDate, Random rng)
        {
            var position = Positions[rng.Next(Positions.Length)];

            // Current ability is modest; better academies (lower tier number) are stronger.
            double currentTarget = 46 - (tier - 1) * 4 + rng.Next(-4, 5);
            var player = PlayerGenerator.GeneratePlayer(
                nationality, position, currentTarget, foreignPlayerChance: 0.1, random: rng, referenceDate: referenceDate);

            int age = rng.Next(15, 20);
            player.Age = age;
            player.DateOfBirth = referenceDate.AddYears(-age).AddDays(-rng.Next(0, 365));
            player.IsYouthProspect = true;
            player.Status = PlayerStatus.Available;
            player.Fitness = rng.Next(80, 96);

            // Potential ceiling: higher tiers scout more talented youngsters.
            int talentFloor = 48 - (tier - 1) * 4;
            int talentCeil = 92 - (tier - 1) * 6;
            int talent = rng.Next(talentFloor, talentCeil + 1);
            player.Talent = Math.Clamp(Math.Max(talent, (int)Math.Round(player.Rating)), 1, 99);

            return player;
        }
    }
}
