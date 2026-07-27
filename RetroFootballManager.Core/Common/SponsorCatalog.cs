using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Fixed pool of sponsors per slot, tiered by league level. Rewritten fresh into the DB on
    // every new game (sponsors are reference data, not save-game progress).
    public static class SponsorCatalog
    {
        // Two options per slot/tier with different risk/reward profiles: a "safe" deal (high
        // base payment, moderate bonuses) and an "ambitious" deal (lower base payment but much
        // higher win/promotion bonus) - a real choice instead of just one option per league level.
        public static List<Sponsor> CreateDefaultCatalog() =>
        [
            new() { Name = "Bundesbank Invest", SponsorType = SponsorType.Main, MinTier = 1, SeasonPayment = 8_000_000, BonusPerWin = 100_000, BonusPerPromotion = 2_000_000 },
            new() { Name = "Global Airlines", SponsorType = SponsorType.Main, MinTier = 1, SeasonPayment = 5_500_000, BonusPerWin = 180_000, BonusPerPromotion = 3_500_000 },
            new() { Name = "MetroTech AG", SponsorType = SponsorType.Main, MinTier = 2, SeasonPayment = 3_000_000, BonusPerWin = 40_000, BonusPerPromotion = 800_000 },
            new() { Name = "NordWest Versicherung", SponsorType = SponsorType.Main, MinTier = 2, SeasonPayment = 2_000_000, BonusPerWin = 70_000, BonusPerPromotion = 1_400_000 },
            new() { Name = "Regio Energie", SponsorType = SponsorType.Main, MinTier = 3, SeasonPayment = 800_000, BonusPerWin = 15_000, BonusPerPromotion = 300_000 },
            new() { Name = "Regio Bau AG", SponsorType = SponsorType.Main, MinTier = 3, SeasonPayment = 500_000, BonusPerWin = 28_000, BonusPerPromotion = 550_000 },
            new() { Name = "Lokal Handel eG", SponsorType = SponsorType.Main, MinTier = 4, SeasonPayment = 200_000, BonusPerWin = 5_000, BonusPerPromotion = 100_000 },
            new() { Name = "Provinz Discounter", SponsorType = SponsorType.Main, MinTier = 4, SeasonPayment = 120_000, BonusPerWin = 9_000, BonusPerPromotion = 180_000 },

            new() { Name = "Stadionbrauerei", SponsorType = SponsorType.Perimeter, MinTier = 1, SeasonPayment = 2_000_000, BonusPerWin = 20_000, BonusPerPromotion = 400_000 },
            new() { Name = "TechStream Streaming", SponsorType = SponsorType.Perimeter, MinTier = 1, SeasonPayment = 1_300_000, BonusPerWin = 35_000, BonusPerPromotion = 650_000 },
            new() { Name = "Autohaus Nord", SponsorType = SponsorType.Perimeter, MinTier = 2, SeasonPayment = 900_000, BonusPerWin = 10_000, BonusPerPromotion = 200_000 },
            new() { Name = "Fitnessstudio PowerFit", SponsorType = SponsorType.Perimeter, MinTier = 2, SeasonPayment = 600_000, BonusPerWin = 18_000, BonusPerPromotion = 320_000 },
            new() { Name = "Sparkasse Regional", SponsorType = SponsorType.Perimeter, MinTier = 3, SeasonPayment = 300_000, BonusPerWin = 5_000, BonusPerPromotion = 80_000 },
            new() { Name = "KFZ Meister Bloch", SponsorType = SponsorType.Perimeter, MinTier = 3, SeasonPayment = 180_000, BonusPerWin = 9_000, BonusPerPromotion = 140_000 },
            new() { Name = "Getränke Schulz", SponsorType = SponsorType.Perimeter, MinTier = 4, SeasonPayment = 80_000, BonusPerWin = 2_000, BonusPerPromotion = 30_000 },
            new() { Name = "Bäckerei Krause", SponsorType = SponsorType.Perimeter, MinTier = 4, SeasonPayment = 50_000, BonusPerWin = 3_500, BonusPerPromotion = 55_000 },

            new() { Name = "Sportwear Global", SponsorType = SponsorType.Kit, MinTier = 1, SeasonPayment = 5_000_000, BonusPerWin = 50_000, BonusPerPromotion = 1_000_000 },
            new() { Name = "ProSport International", SponsorType = SponsorType.Kit, MinTier = 1, SeasonPayment = 3_400_000, BonusPerWin = 85_000, BonusPerPromotion = 1_600_000 },
            new() { Name = "Trikot & Co", SponsorType = SponsorType.Kit, MinTier = 2, SeasonPayment = 1_500_000, BonusPerWin = 20_000, BonusPerPromotion = 400_000 },
            new() { Name = "SprintWear", SponsorType = SponsorType.Kit, MinTier = 2, SeasonPayment = 1_000_000, BonusPerWin = 35_000, BonusPerPromotion = 650_000 },
        ];
    }
}
