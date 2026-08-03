using RetroFootballManager.Core.Models;
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
                // ---------------------------------------------------------
                // LIGA 1 – MAIN SPONSORS
                // ---------------------------------------------------------

                new() { Name = "Imperial Capital Group", SponsorType = SponsorType.Main, MinTier = 1,
                    SeasonPayment = 12_000_000, HasNoBonus = true, BonusTypes = [BonusType.None],
                    BonusPerWin = 1_000 },

                new() { Name = "Bundesbank Invest", SponsorType = SponsorType.Main, MinTier = 1,
                    SeasonPayment = 8_000_000, BonusTypes = [BonusType.Top5, BonusType.MasterTitle],
                    BonusPerWin = 100_000, BonusPerPromotion = 0,
                    BonusForTop5 = 500_000, BonusForMasterTitle = 2_000_000 },

                new() { Name = "Global Airlines", SponsorType = SponsorType.Main, MinTier = 1,
                    SeasonPayment = 5_500_000, BonusTypes = [BonusType.Midfield, BonusType.Top5],
                    BonusPerWin = 180_000, BonusPerPromotion = 0,
                    BonusForMidfieldPlace = 300_000, BonusForTop5 = 700_000 },

                new() { Name = "EuroFinance Group", SponsorType = SponsorType.Main, MinTier = 1,
                    SeasonPayment = 6_500_000, BonusTypes = [BonusType.Top5, BonusType.MasterTitle],
                    BonusPerWin = 120_000, BonusPerPromotion = 0,
                    BonusForTop5 = 400_000, BonusForMasterTitle = 1_500_000 },

                new() { Name = "PrimeTel Communications", SponsorType = SponsorType.Main, MinTier = 1,
                    SeasonPayment = 5_800_000, BonusTypes = [BonusType.Midfield, BonusType.Top5],
                    BonusPerWin = 150_000, BonusPerPromotion = 0,
                    BonusForMidfieldPlace = 300_000, BonusForTop5 = 600_000 },

                // ---------------------------------------------------------
                // LIGA 2 – MAIN SPONSORS
                // ---------------------------------------------------------

                new() { Name = "Nordic Holding AG", SponsorType = SponsorType.Main, MinTier = 2,
                    SeasonPayment = 4_200_000, HasNoBonus = true, BonusTypes = [BonusType.None],
                    BonusPerWin = 1_000 },

                new() { Name = "MetroTech AG", SponsorType = SponsorType.Main, MinTier = 2,
                    SeasonPayment = 3_000_000, BonusTypes = [BonusType.Top5, BonusType.PerPromotion],
                    BonusPerWin = 40_000, BonusPerPromotion = 800_000,
                    BonusForTop5 = 250_000 },

                new() { Name = "NordWest Versicherung", SponsorType = SponsorType.Main, MinTier = 2,
                    SeasonPayment = 2_000_000, BonusTypes = [BonusType.Midfield, BonusType.PerPromotion],
                    BonusPerWin = 70_000, BonusPerPromotion = 1_400_000,
                    BonusForMidfieldPlace = 200_000 },

                new() { Name = "Urban Mobility AG", SponsorType = SponsorType.Main, MinTier = 2,
                    SeasonPayment = 2_700_000, BonusTypes = [BonusType.Top5, BonusType.MasterTitle],
                    BonusPerWin = 55_000, BonusForMasterTitle = 900_000,
                    BonusForTop5 = 300_000 },

                new() { Name = "Nordlicht Telekom", SponsorType = SponsorType.Main, MinTier = 2,
                    SeasonPayment = 2_300_000, BonusTypes = [BonusType.Midfield, BonusType.MasterTitle],
                    BonusPerWin = 60_000, BonusForMasterTitle = 1_100_000,
                    BonusForMidfieldPlace = 200_000 },

                // ---------------------------------------------------------
                // LIGA 3 – MAIN SPONSORS
                // ---------------------------------------------------------

                new() { Name = "Stadtwerke Südpark", SponsorType = SponsorType.Main, MinTier = 3,
                    SeasonPayment = 1_200_000, HasNoBonus = true, BonusTypes = [BonusType.None],
                    BonusPerWin = 1_000 },

                new() { Name = "Regio Energie", SponsorType = SponsorType.Main, MinTier = 3,
                    SeasonPayment = 800_000, BonusTypes = [BonusType.Midfield, BonusType.PerPromotion],
                    BonusPerWin = 15_000, BonusPerPromotion = 300_000,
                    BonusForMidfieldPlace = 80_000 },

                new() { Name = "Regio Bau AG", SponsorType = SponsorType.Main, MinTier = 3,
                    SeasonPayment = 500_000, BonusTypes = [BonusType.Top5, BonusType.MasterTitle],
                    BonusPerWin = 28_000, BonusForMasterTitle = 550_000,
                    BonusForTop5 = 120_000 },

                new() { Name = "Handelskontor Müller", SponsorType = SponsorType.Main, MinTier = 3,
                    SeasonPayment = 700_000, BonusTypes = [BonusType.Midfield, BonusType.MasterTitle],
                    BonusPerWin = 20_000, BonusForMasterTitle = 250_000,
                    BonusForMidfieldPlace = 80_000 },

                new() { Name = "OptiMed Pharma", SponsorType = SponsorType.Main, MinTier = 3,
                    SeasonPayment = 500_000, BonusTypes = [BonusType.Top5, BonusType.PerPromotion],
                    BonusPerWin = 25_000, BonusPerPromotion = 300_000,
                    BonusForTop5 = 120_000 },

                // ---------------------------------------------------------
                // LIGA 4 – MAIN SPONSORS
                // ---------------------------------------------------------

                new() { Name = "Landmarkt Union", SponsorType = SponsorType.Main, MinTier = 4, ImagePath="landmarkt_union.png",
                    SeasonPayment = 350_000, HasNoBonus = true, BonusTypes = [BonusType.None],
                    BonusPerWin = 1_000 },

                new() { Name = "Lokal Handel eG", SponsorType = SponsorType.Main, MinTier = 4, ImagePath = "lokalhandel_eg.png",
                    SeasonPayment = 200_000, BonusTypes = [BonusType.PerPromotion, BonusType.Midfield],
                    BonusPerWin = 5_000, BonusPerPromotion = 100_000,
                    BonusForMidfieldPlace = 20_000 },

                new() { Name = "Provinz Discounter", SponsorType = SponsorType.Main, MinTier = 4, ImagePath="provinz_discounter.png",
                    SeasonPayment = 120_000, BonusTypes = [BonusType.Top5, BonusType.PerPromotion],
                    BonusPerWin = 9_000, BonusPerPromotion = 180_000,
                    BonusForTop5 = 120_000 },

                new() { Name = "Getränke Meyer", SponsorType = SponsorType.Main, MinTier = 4, ImagePath="getraenke_meyer.png",
                    SeasonPayment = 180_000, BonusTypes = [BonusType.Midfield, BonusType.PerPromotion],
                    BonusPerWin = 6_000, BonusPerPromotion = 70_000,
                    BonusForMidfieldPlace = 20_000 },

                new() { Name = "AutoService Klein", SponsorType = SponsorType.Main, MinTier = 4, ImagePath = "autoservice_klein.png",
                    SeasonPayment = 150_000, BonusTypes = [BonusType.Top5, BonusType.MasterTitle],
                    BonusPerWin = 5_500, BonusForMasterTitle = 85_000,
                    BonusForTop5 = 120_000 },

                // ---------------------------------------------------------
                // PERIMETER SPONSORS
                // Pitch-side ad boards are worth far less than a main/kit deal -
                // roughly 15% of the tier's main-sponsor payment, scaled down through the tiers.
                // ---------------------------------------------------------

                // LIGA 1
                new() { Name = "Premium Getränke AG", SponsorType = SponsorType.Perimeter, MinTier = 1,
                    SeasonPayment = 1_500_000, HasNoBonus = true, BonusTypes = [BonusType.None],
                    BonusPerWin = 1_000 },

                new() { Name = "Stadionbrauerei", SponsorType = SponsorType.Perimeter, MinTier = 1,
                    SeasonPayment = 1_100_000, BonusTypes = [BonusType.PerWin, BonusType.Top5],
                    BonusPerWin = 11_000, BonusForTop5 = 320_000 },

                new() { Name = "TechStream Streaming", SponsorType = SponsorType.Perimeter, MinTier = 1,
                    SeasonPayment = 700_000, BonusTypes = [BonusType.PerWin, BonusType.MasterTitle],
                    BonusPerWin = 19_000, BonusForMasterTitle = 800_000 },

                new() { Name = "ArenaSnack Europe", SponsorType = SponsorType.Perimeter, MinTier = 1,
                    SeasonPayment = 850_000, BonusTypes = [BonusType.Top5, BonusType.MasterTitle],
                    BonusForTop5 = 375_000, BonusForMasterTitle = 950_000 },

                // LIGA 2
                new() { Name = "Nordlicht Getränke GmbH", SponsorType = SponsorType.Perimeter, MinTier = 2,
                    SeasonPayment = 550_000, HasNoBonus = true, BonusTypes = [BonusType.None] },

                new() { Name = "Autohaus Nord", SponsorType = SponsorType.Perimeter, MinTier = 2,
                    SeasonPayment = 410_000, BonusTypes = [BonusType.PerWin, BonusType.PerPromotion],
                    BonusPerWin = 4_500, BonusPerPromotion = 275_000 },

                new() { Name = "Fitnessstudio PowerFit", SponsorType = SponsorType.Perimeter, MinTier = 2,
                    SeasonPayment = 275_000, BonusTypes = [BonusType.Top5, BonusType.MasterTitle],
                    BonusForTop5 = 160_000, BonusForMasterTitle = 410_000 },

                new() { Name = "ReifenProfi24", SponsorType = SponsorType.Perimeter, MinTier = 2,
                    SeasonPayment = 320_000, BonusTypes = [BonusType.PerWin, BonusType.Top5],
                    BonusPerWin = 5_500, BonusForTop5 = 140_000 },

                // LIGA 3
                new() { Name = "Frischemarkt Lenz", SponsorType = SponsorType.Perimeter, MinTier = 3,
                    SeasonPayment = 170_000, HasNoBonus = true, BonusTypes = [BonusType.None] },

                new() { Name = "Sparkasse Regional", SponsorType = SponsorType.Perimeter, MinTier = 3,
                    SeasonPayment = 100_000, BonusTypes = [BonusType.PerWin, BonusType.PerPromotion],
                    BonusPerWin = 2_000, BonusPerPromotion = 100_000 },

                new() { Name = "KFZ Meister Bloch", SponsorType = SponsorType.Perimeter, MinTier = 3,
                    SeasonPayment = 60_000, BonusTypes = [BonusType.Top5, BonusType.MasterTitle],
                    BonusForTop5 = 70_000, BonusForMasterTitle = 200_000 },

                new() { Name = "Holz & Haus GmbH", SponsorType = SponsorType.Perimeter, MinTier = 3,
                    SeasonPayment = 75_000, BonusTypes = [BonusType.PerWin, BonusType.Top5],
                    BonusPerWin = 3_000, BonusForTop5 = 60_000 },

                // LIGA 4
                new() { Name = "Dorfmarkt Schröder", SponsorType = SponsorType.Perimeter, MinTier = 4, ImagePath="dorfmarkt_schoeder.png",
                    SeasonPayment = 55_000, BonusPerWin = 3_000, HasNoBonus = true, BonusTypes = [BonusType.None] },

                new() { Name = "Getränke Schulz", SponsorType = SponsorType.Perimeter, MinTier = 4, ImagePath="getraenke_schulz.png",
                    SeasonPayment = 22_000, BonusTypes = [BonusType.PerPromotion],
                    BonusPerWin = 1500, BonusPerPromotion = 40_000 },

                new() { Name = "Bäckerei Krause", SponsorType = SponsorType.Perimeter, MinTier = 4, ImagePath="baeckerei_krause.png",
                    SeasonPayment = 14_000, BonusTypes = [BonusType.Top5, BonusType.MasterTitle],
                    BonusForTop5 = 33_000, BonusPerWin= 2_000, BonusForMasterTitle = 80_000 },

                new() { Name = "Fahrrad Müller", SponsorType = SponsorType.Perimeter, MinTier = 4, ImagePath = "fahrrad_mueller.png",
                    SeasonPayment = 25_000, BonusTypes = [BonusType.Top5],
                    BonusPerWin = 1_000, BonusForTop5 = 50_000 },

                // ---------------------------------------------------------
                // KIT SPONSORS
                // ---------------------------------------------------------

                // LIGA 1
                new() { Name = "EliteGear Sports", SponsorType = SponsorType.Kit, MinTier = 1,
                    SeasonPayment = 6_500_000, BonusPerWin=60_000, HasNoBonus = true, BonusTypes = [BonusType.None] },

                new() { Name = "Sportwear Global", SponsorType = SponsorType.Kit, MinTier = 1,
                    SeasonPayment = 5_000_000, BonusTypes = [BonusType.MasterTitle],
                    BonusPerWin = 50_000, BonusForMasterTitle = 2_000_000 },

                new() { Name = "ProSport International", SponsorType = SponsorType.Kit, MinTier = 1,
                    SeasonPayment = 3_400_000, BonusTypes = [BonusType.Top5, BonusType.MasterTitle],
                    BonusForTop5 = 800_000, BonusForMasterTitle = 1_800_000 },

                new() { Name = "ChampionWear Global", SponsorType = SponsorType.Kit, MinTier = 1,
                    SeasonPayment = 4_200_000, BonusTypes = [BonusType.Top5],
                    BonusPerWin = 85_000, BonusForTop5 = 900_000 },

                // LIGA 2
                new() { Name = "TeamLine Sports", SponsorType = SponsorType.Kit, MinTier = 2,
                    SeasonPayment = 2_200_000, BonusPerWin = 25_000, HasNoBonus = true, BonusTypes = [BonusType.None] },

                new() { Name = "Trikot & Co", SponsorType = SponsorType.Kit, MinTier = 2,
                    SeasonPayment = 1_500_000, BonusTypes = [BonusType.PerPromotion],
                    BonusPerWin = 20_000, BonusPerPromotion = 400_000 },

                new() { Name = "SprintWear", SponsorType = SponsorType.Kit, MinTier = 2,
                    SeasonPayment = 1_000_000, BonusTypes = [BonusType.Top5, BonusType.MasterTitle],
                    BonusForTop5 = 300_000, BonusPerWin=40_000, BonusForMasterTitle = 700_000 },

                new() { Name = "Athletica Outfitters", SponsorType = SponsorType.Kit, MinTier = 2,
                    SeasonPayment = 900_000, BonusTypes = [BonusType.Top5],
                    BonusPerWin = 35_000, BonusForTop5 = 250_000 },
            ];
     }

}
