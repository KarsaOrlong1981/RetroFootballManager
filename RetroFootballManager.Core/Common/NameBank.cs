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
            public List<string> LastNames { get; set; } = [];
        }
    }
}
