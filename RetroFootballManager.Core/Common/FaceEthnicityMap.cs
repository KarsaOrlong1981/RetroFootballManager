using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Maps Nationality to the curated NG_Regens portrait bucket(s) under
    // Resources/Raw/Faces, so a player/staff photo actually matches the look implied by
    // their name/nationality (see FaceImageAssigner). Nationality.International has no
    // dedicated folder - like NameBank, it draws from the full bucket list instead.
    public static class FaceEthnicityMap
    {
        public static readonly string[] AllPlayerBuckets =
        [
            "CentralEurope", "Anglosphere", "Italia", "Spain", "France", "Netherlands",
            "Scandinavian", "Finstonia", "Iceland", "Ireland", "BrazilMixed",
            "SouthConeSA", "Japan", "WestAfrica", "EasternEurope",
        ];

        private static readonly Dictionary<Nationality, string> PlayerBucketByNationality = new()
        {
            [Nationality.Germany] = "CentralEurope",
            [Nationality.Austria] = "CentralEurope",
            [Nationality.England] = "Anglosphere",
            [Nationality.Scotland] = "Anglosphere",
            [Nationality.Italy] = "Italia",
            [Nationality.Spain] = "Spain",
            [Nationality.France] = "France",
            [Nationality.Netherlands] = "Netherlands",
            [Nationality.Belgium] = "Netherlands",
            [Nationality.Denmark] = "Scandinavian",
            [Nationality.Norway] = "Scandinavian",
            [Nationality.Finland] = "Finstonia",
            [Nationality.Iceland] = "Iceland",
            [Nationality.Ireland] = "Ireland",
            [Nationality.Brazil] = "BrazilMixed",
            [Nationality.Argentina] = "SouthConeSA",
            [Nationality.Japan] = "Japan",
            [Nationality.Nigeria] = "WestAfrica",
            [Nationality.EasternEurope] = "EasternEurope",
        };

        public static string GetPlayerBucket(Nationality nationality, Random rng) =>
            PlayerBucketByNationality.TryGetValue(nationality, out var bucket)
                ? bucket
                : AllPlayerBuckets[rng.Next(AllPlayerBuckets.Length)];

        public static readonly string[] AllStaffRegions =
        [
            "NorthCentralEurope", "NorthWestEurope", "SouthEurope", "SouthWestEurope",
            "EasternEurope", "Brazil", "LatinAmerica", "African", "Japan",
        ];

        private static readonly Dictionary<Nationality, string> StaffRegionByNationality = new()
        {
            [Nationality.Germany] = "NorthCentralEurope",
            [Nationality.Austria] = "NorthCentralEurope",
            [Nationality.Netherlands] = "NorthCentralEurope",
            [Nationality.Belgium] = "NorthCentralEurope",
            [Nationality.Denmark] = "NorthCentralEurope",
            [Nationality.Norway] = "NorthCentralEurope",
            [Nationality.Finland] = "NorthCentralEurope",
            [Nationality.Iceland] = "NorthCentralEurope",
            [Nationality.England] = "NorthWestEurope",
            [Nationality.Scotland] = "NorthWestEurope",
            [Nationality.Ireland] = "NorthWestEurope",
            [Nationality.Italy] = "SouthEurope",
            [Nationality.Spain] = "SouthWestEurope",
            [Nationality.France] = "SouthWestEurope",
            [Nationality.EasternEurope] = "EasternEurope",
            [Nationality.Brazil] = "Brazil",
            [Nationality.Argentina] = "LatinAmerica",
            [Nationality.Nigeria] = "African",
            [Nationality.Japan] = "Japan",
        };

        public static string GetStaffRegion(Nationality nationality, Random rng) =>
            StaffRegionByNationality.TryGetValue(nationality, out var region)
                ? region
                : AllStaffRegions[rng.Next(AllStaffRegions.Length)];
    }
}
