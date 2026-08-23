using System.Reflection;
using System.Text.Json;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Loads first-/last-name pools per nationality from an embedded JSON resource
    // and returns randomly combined, plausible-sounding player names from them.
    public static class NameBank
    {
        private const string ResourceName = "RetroFootballManager.Core.Data.Names.PlayerNames.json";

        private static readonly Lazy<Dictionary<Nationality, NamePool>> Pools = new(Load);

        public static (string FirstName, string LastName) GetRandomName(Nationality nationality, Random random)
        {
            var pool = Pools.Value.TryGetValue(nationality, out var found)
                ? found
                : Pools.Value[Nationality.International];

            string first = pool.FirstNames[random.Next(pool.FirstNames.Count)];
            string last = pool.LastNames[random.Next(pool.LastNames.Count)];
            return (first, last);
        }

        // Staff can be any gender (see StaffGenerator) - picks from FemaleFirstNames so a
        // female employee never ends up with a male first name (and vice versa). Falls back
        // to the (male) FirstNames pool for nationalities without a FemaleFirstNames list yet.
        public static (string FirstName, string LastName) GetRandomName(Nationality nationality, Gender gender, Random random)
        {
            var pool = Pools.Value.TryGetValue(nationality, out var found)
                ? found
                : Pools.Value[Nationality.International];

            var firstNames = gender == Gender.Female && pool.FemaleFirstNames.Count > 0
                ? pool.FemaleFirstNames
                : pool.FirstNames;

            string first = firstNames[random.Next(firstNames.Count)];
            string last = pool.LastNames[random.Next(pool.LastNames.Count)];
            return (first, last);
        }

        // Used to self-heal existing employees generated while Gender was rolled independently
        // of the name pool (see StaffGenerator.FixGenderNameMismatch). Only reliable for
        // nationalities with a FemaleFirstNames list - always false otherwise.
        public static bool IsFemaleFirstName(Nationality nationality, string firstName)
        {
            var pool = Pools.Value.TryGetValue(nationality, out var found)
                ? found
                : Pools.Value[Nationality.International];
            return pool.FemaleFirstNames.Contains(firstName);
        }

        private static Dictionary<Nationality, NamePool> Load()
        {
            var assembly = typeof(NameBank).Assembly;
            using var stream = assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException($"Eingebettete Ressource '{ResourceName}' wurde nicht gefunden.");

            var raw = JsonSerializer.Deserialize<Dictionary<string, NamePool>>(stream)
                ?? throw new InvalidOperationException("Namenspools konnten nicht gelesen werden.");

            var result = new Dictionary<Nationality, NamePool>();
            foreach (var (key, value) in raw)
            {
                if (Enum.TryParse<Nationality>(key, out var nationality))
                    result[nationality] = value;
            }

            if (!result.ContainsKey(Nationality.International))
                throw new InvalidOperationException("Der Fallback-Namenspool 'International' fehlt.");

            return result;
        }

        private class NamePool
        {
            public List<string> FirstNames { get; set; } = [];
            public List<string> FemaleFirstNames { get; set; } = [];
            public List<string> LastNames { get; set; } = [];
        }
    }
}
