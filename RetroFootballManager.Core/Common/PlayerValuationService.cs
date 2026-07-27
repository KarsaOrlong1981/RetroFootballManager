using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Unified market value/salary estimation for players (contract seeding, transfer market AI,
    // market display) - tiered by rating with hard caps per band, modulated by talent (potential)
    // and age (peak ~25-29, decline after). Uncapped from rating 80 up.
    public static class PlayerValuationService
    {
        public static double EstimateMarketValue(Player player)
        {
            double talentFactor = 0.75 + (Math.Clamp(player.Talent, 0, 99) / 99.0) * 0.5; // 0.75 .. 1.25
            double ageFactor = AgeFactor(player.Age);

            double raw = BaseByRating(player.Rating) * talentFactor * ageFactor;
            double cap = CapByRating(player.Rating);
            return Math.Round(Math.Min(raw, cap));
        }

        // Annual salary as a share of market value (rough realism anchor: top players rarely earn
        // more than ~15-20% of their market value per year in reality) - prevents low-rated
        // squads from blowing the budget within a few matchdays (see historical bug: the previous
        // rating²×250 formula produced >13M salary/season for league-4 squads).
        public static double EstimateAnnualSalary(Player player) =>
            Math.Round(EstimateMarketValue(player) * 0.15);

        private static double BaseByRating(double rating) => rating switch
        {
            <= 60 => Math.Pow(rating / 60.0, 2) * 950_000,
            <= 70 => 950_000 + (rating - 60) / 10.0 * 2_950_000,
            <= 80 => 3_900_000 + (rating - 70) / 10.0 * 16_000_000,
            _ => 19_900_000 + Math.Pow(rating - 80, 2) * 400_000,
        };

        private static double CapByRating(double rating) => rating switch
        {
            <= 60 => 999_000,
            <= 70 => 3_999_000,
            <= 80 => 19_999_000,
            _ => double.MaxValue,
        };

        private static double AgeFactor(int age) => age switch
        {
            <= 20 => 0.85,
            <= 29 => 1.0,
            <= 33 => 1.0 - (age - 29) * 0.08,
            _ => Math.Max(0.25, 0.68 - (age - 33) * 0.1),
        };
    }
}
