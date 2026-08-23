using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Fictional European clubs for the Champions League (M6c) and Europa Cup (M6d) - pure
    // opponent teams, never managed by the player (no youth squad/staff/stadium upgrades).
    public static class ForeignClubGenerator
    {
        public enum Competition { ChampionsLeague, EuropaCup }

        // Europa Cup is overall weaker than the Champions League (plan: -15 to -20).
        private const double EuropaCupRatingOffset = -18;

        private record CountryGroup(Nationality Nationality, int Count, double BaseRating);

        // Pot assignment is derived later purely from rating (GroupDrawService) - this just
        // sets the country distribution per spec: 4x top countries, 2x mid-table,
        // 4x Eastern Europe, 1x each Finland/Iceland/Ireland/Scotland (the German qualifiers
        // replace part of this last group, see GroupDrawService).
        private static List<CountryGroup> CountryGroups(Competition competition)
        {
            double offset = competition == Competition.EuropaCup ? EuropaCupRatingOffset : 0;
            return
            [
                new(Nationality.Spain, 4, 82 + offset),
                new(Nationality.Italy, 4, 82 + offset),
                new(Nationality.England, 4, 82 + offset),
                new(Nationality.France, 4, 82 + offset),
                new(Nationality.Denmark, 2, 70 + offset),
                new(Nationality.Norway, 2, 70 + offset),
                new(Nationality.Netherlands, 2, 70 + offset),
                new(Nationality.Belgium, 2, 70 + offset),
                new(Nationality.EasternEurope, 4, 62 + offset),
                new(Nationality.Finland, 1, 55 + offset),
                new(Nationality.Iceland, 1, 55 + offset),
                new(Nationality.Ireland, 1, 55 + offset),
                new(Nationality.Scotland, 1, 55 + offset),
            ];
        }

        private static readonly Dictionary<Nationality, string[]> CityStems = new()
        {
            [Nationality.Spain] = ["Villoreal", "Sevillo Norte", "Costa Brava", "Andalucía", "Toledana"],
            [Nationality.Italy] = ["Lombardia", "Toscana", "Piemonte", "Umbria", "Liguria"],
            [Nationality.England] = ["Northgate", "Eastbrook", "Millwood", "Ashford", "Kingsmere"],
            [Nationality.France] = ["Provence", "Occitane", "Loiret", "Vendée", "Bretagne"],
            [Nationality.Denmark] = ["Nordhavn", "Silkeborg Vest", "Fjordby", "Østerled"],
            [Nationality.Norway] = ["Fjellheim", "Nordkyst", "Sognedal", "Vestfjord"],
            [Nationality.Netherlands] = ["Waterland", "Zuiderpolder", "Noordwijk Stad", "Vechtdal"],
            [Nationality.Belgium] = ["Wallonia", "Vlaamse Kust", "Ardennen", "Kempenland"],
            [Nationality.EasternEurope] = ["Karpaty", "Nadvirna", "Bilhorod", "Zorya Polis", "Vysoke Pole"],
            [Nationality.Finland] = ["Pohjola"],
            [Nationality.Iceland] = ["Fjallabyggð"],
            [Nationality.Ireland] = ["Cluain Meala"],
            [Nationality.Scotland] = ["Glenmoray"],
        };

        private static readonly string[] Suffixes =
            ["FC", "United", "Athletic", "SC", "City", "Rangers", "Sporting", "Wanderers"];

        public static List<Team> GenerateClubs(Competition competition, Random? random = null)
        {
            var rng = random ?? Random.Shared;
            var teams = new List<Team>();
            var usedNames = new HashSet<string>();

            foreach (var group in CountryGroups(competition))
            {
                for (int i = 0; i < group.Count; i++)
                    teams.Add(CreateClub(group.Nationality, group.BaseRating, rng, usedNames));
            }

            return teams;
        }

        private static Team CreateClub(Nationality nationality, double targetRating, Random rng, HashSet<string> usedNames)
        {
            double teamTarget = targetRating + (rng.NextDouble() * 8 - 4);
            string name = UniqueName(nationality, rng, usedNames);
            string shortName = name.Length <= 3 ? name.ToUpperInvariant() : name[..3].ToUpperInvariant();

            var players = PlayerGenerator.GenerateSquad(
                nationality, teamTarget, squadSize: 22, foreignPlayerChance: 0.1, random: rng,
                referenceDate: new DateTime(2026, 8, 1));
            FaceImageAssigner.AssignPlayerFaces(players, rng);

            var team = new Team
            {
                Name = name,
                ShortName = shortName,
                Nationality = nationality,
                LeagueTier = 0, // not a German league team
                FormationName = "4-4-2",
                PlayingStyle = RandomPlayingStyle(rng),
                TacticalOrientation = TacticalOrientation.Balanced,
                TacklingIntensity = TacklingIntensity.Normal,
                Players = players,
                Statistics = new TeamStats(),
                Stadium = new Stadium
                {
                    Name = $"{name} Arena",
                    SeatingCapacity = 30_000,
                    StandingCapacity = 5_000,
                    LogeCapacity = 500,
                    Condition = rng.Next(60, 90),
                    Atmosphere = rng.Next(50, 90),
                    HomeAdvantage = rng.Next(40, 70),
                },
            };

            LineupSelector.SelectLineup(team);
            return team;
        }

        private static string UniqueName(Nationality nationality, Random rng, HashSet<string> usedNames)
        {
            var stems = CityStems[nationality];
            for (int attempt = 0; attempt < 50; attempt++)
            {
                string stem = stems[rng.Next(stems.Length)];
                string suffix = Suffixes[rng.Next(Suffixes.Length)];
                string name = $"{stem} {suffix}";
                if (usedNames.Add(name))
                    return name;
            }

            // Fallback (name pool exhausted) - a running number guarantees uniqueness.
            string fallback = $"{stems[0]} FC {usedNames.Count}";
            usedNames.Add(fallback);
            return fallback;
        }

        private static PlayingStyle RandomPlayingStyle(Random rng)
        {
            var values = Enum.GetValues<PlayingStyle>();
            return values[rng.Next(values.Length)];
        }
    }
}
