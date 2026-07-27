using RetroFootballManager.Models;

namespace RetroFootballManager.Services
{
    public static class TrophyDisplay
    {
        public static readonly TrophyType[] All =
        [
            TrophyType.DeutscherMeister,
            TrophyType.MeisterLiga2,
            TrophyType.MeisterLiga3,
            TrophyType.MeisterLiga4,
            TrophyType.DeutscherPokal,
            TrophyType.EuropaPokalDerMeister,
            TrophyType.EuropaPokal,
        ];

        public static string Label(TrophyType type) => type switch
        {
            TrophyType.DeutscherMeister => "Deutscher Meister",
            TrophyType.MeisterLiga2 => "Meister Liga 2",
            TrophyType.MeisterLiga3 => "Meister Liga 3",
            TrophyType.MeisterLiga4 => "Meister Liga 4",
            TrophyType.DeutscherPokal => "Deutscher Pokal",
            TrophyType.EuropaPokalDerMeister => "Europa Pokal der Meister",
            TrophyType.EuropaPokal => "Europa Cup",
            _ => type.ToString(),
        };

        public static string ImageFileName(TrophyType type) => type switch
        {
            TrophyType.DeutscherMeister => "trophy_deutscher_meister.jpg",
            TrophyType.MeisterLiga2 => "trophy_meister_liga2.jpg",
            TrophyType.MeisterLiga3 => "trophy_meister_liga3.jpg",
            TrophyType.MeisterLiga4 => "trophy_meister_liga4.jpg",
            TrophyType.DeutscherPokal => "trophy_deutscher_pokal.jpg",
            TrophyType.EuropaPokalDerMeister => "trophy_europa_pokal_der_meister.jpg",
            TrophyType.EuropaPokal => "trophy_europa_pokal.jpg",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }
}
