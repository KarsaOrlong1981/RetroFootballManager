using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Assigns NewGen portrait images (see Resources/Raw/Faces, bundled as MauiAsset and
    // deployed as loose files next to the app on Windows) to players/staff so photos match
    // the ethnicity implied by their Nationality. The face pack only contains young-looking
    // generated faces: players above MaxFaceAge get no image (UI falls back to avatar.png),
    // but staff always get one regardless of age - there's no older-looking pack yet, so
    // staff photos will look youthful until that's added later.
    public static class FaceImageAssigner
    {
        public const int MaxFaceAge = 25;

        // Overridable so tests can point at a temp folder instead of the real app output
        // directory (Core has no MAUI reference; raw MauiAsset files just land next to the
        // .exe on Windows, verified via Resources/Raw/AboutAssets.txt's existing pattern).
        public static string FacesRootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "Faces");

        // Directory listings never change at runtime - cached once per bucket folder.
        private static readonly Dictionary<string, string[]> DirectoryCache = new();

        // Assigns a face to every player in the list who doesn't have one yet and is young
        // enough for the pack. Dedupes within the list so the same squad never shows the same
        // photo twice; the (curated, finite) pool can still repeat across different squads.
        public static void AssignPlayerFaces(IEnumerable<Player> players, Random rng)
        {
            var squad = players as IReadOnlyCollection<Player> ?? players.ToList();
            var used = new HashSet<string>(
                squad.Select(p => p.ImagePath).Where(p => !string.IsNullOrEmpty(p))!);

            foreach (var player in squad)
            {
                if (player.ImagePath is not null || player.Age > MaxFaceAge)
                    continue;

                string bucket = FaceEthnicityMap.GetPlayerBucket(player.Nationality, rng);
                string? path = PickFace(Path.Combine(FacesRootPath, "Players", bucket), rng, used);
                if (path is null)
                    continue;

                used.Add(path);
                player.ImagePath = path;
            }
        }

        // Same idea as AssignPlayerFaces, but also picks the folder (male/female) from each
        // employee's Gender (rolled by StaffGenerator alongside Age/Nationality). Unlike
        // players, staff always get a photo regardless of age (see class comment).
        public static void AssignStaffFaces(IEnumerable<Employee> employees, Random rng)
        {
            var staff = employees as IReadOnlyCollection<Employee> ?? employees.ToList();
            var used = new HashSet<string>(
                staff.Select(e => e.ImagePath).Where(p => !string.IsNullOrEmpty(p))!);

            foreach (var employee in staff)
            {
                if (employee.ImagePath is not null)
                    continue;

                string region = FaceEthnicityMap.GetStaffRegion(employee.Nationality, rng);
                string genderFolder = employee.Gender == Gender.Female ? "female" : "male";
                string? path = PickFace(Path.Combine(FacesRootPath, "Staff", region, genderFolder), rng, used);
                if (path is null)
                    continue;

                used.Add(path);
                employee.ImagePath = path;
            }
        }

        private static string? PickFace(string directory, Random rng, HashSet<string> used)
        {
            if (!DirectoryCache.TryGetValue(directory, out var files))
            {
                files = Directory.Exists(directory) ? Directory.GetFiles(directory) : [];
                DirectoryCache[directory] = files;
            }
            if (files.Length == 0)
                return null;

            var unused = files.Where(f => !used.Contains(f)).ToArray();
            var pool = unused.Length > 0 ? unused : files;
            return pool[rng.Next(pool.Length)];
        }
    }
}
