using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Pot draw + group stage schedule for Champions League/Europa Cup (M6c/M6d) and the
    // transition into the knockout bracket (reuses CupDrawService.BuildNextRound from the
    // round of 16 on - home advantage there is already purely random, matching "pure
    // tournament strength instead of league tier").
    public static class GroupDrawService
    {
        private static readonly string[] GroupNames = ["A", "B", "C", "D", "E", "F", "G", "H"];
        private static readonly Nationality[] SmallPoolNations =
            [Nationality.Finland, Nationality.Iceland, Nationality.Ireland, Nationality.Scotland];

        // Replaces the weakest Finland/Iceland/Ireland/Scotland clubs with the German
        // qualifiers (4 for CL, 3 for Europa Cup), so the total stays exactly 32.
        public static List<Team> BuildParticipants(IReadOnlyList<Team> foreignClubs, IReadOnlyList<Team> germanQualifiers)
        {
            var removable = foreignClubs
                .Where(c => SmallPoolNations.Contains(c.Nationality))
                .OrderBy(c => c.AverageRating)
                .Take(germanQualifiers.Count)
                .ToHashSet();

            return foreignClubs.Where(c => !removable.Contains(c)).Concat(germanQualifiers).ToList();
        }

        // 4 pots of 8 (sorted by rating, pot 1 = strongest 8), exactly one team per pot per
        // group ("snake draft" across the 8 groups A..H).
        public static Dictionary<string, List<Team>> DrawGroups(IReadOnlyList<Team> participants, Random? random = null)
        {
            if (participants.Count != 32)
                throw new ArgumentException("Erwartet genau 32 Teilnehmer.", nameof(participants));

            var rng = random ?? Random.Shared;
            var sorted = participants.OrderByDescending(t => t.AverageRating).ToList();

            var groups = GroupNames.ToDictionary(g => g, _ => new List<Team>());
            for (int pot = 0; pot < 4; pot++)
            {
                var potTeams = sorted.Skip(pot * 8).Take(8).OrderBy(_ => rng.Next()).ToList();
                for (int g = 0; g < 8; g++)
                    groups[GroupNames[g]].Add(potTeams[g]);
            }

            return groups;
        }

        // Home/away rounds per group (4 teams, 6 matchdays of 2 matches = 12 matches per group).
        public static List<CupTie> BuildGroupStageFixtures(
            Dictionary<string, List<Team>> groups, CompetitionType competition, int season, DateTime firstMatchday)
        {
            var ties = new List<CupTie>();

            foreach (var (groupName, teams) in groups)
            {
                var rounds = RoundRobinRounds(teams.Select(t => t.Id).ToList());
                for (int roundIndex = 0; roundIndex < rounds.Count; roundIndex++)
                {
                    var date = firstMatchday.AddDays(roundIndex * 14); // one group matchday every 2 weeks
                    int matchNumber = 1;
                    foreach (var (home, away) in rounds[roundIndex])
                    {
                        ties.Add(new CupTie
                        {
                            CompetitionType = competition,
                            Season = season,
                            Round = 0, // group stage (sentinel - knockout rounds start at CupDrawService.RoundLastSixteen)
                            MatchNumberInRound = matchNumber++,
                            Group = groupName,
                            HomeTeamId = home,
                            AwayTeamId = away,
                            Date = date,
                        });
                    }
                }
            }

            return ties;
        }

        // Round-robin for a 4-team group: 3 rounds first half + 3 rounds second half (home/
        // away swapped) = 6 matchdays, 2 matches per matchday.
        private static List<List<(int Home, int Away)>> RoundRobinRounds(List<int> teamIds)
        {
            int n = teamIds.Count;
            var list = new List<int>(teamIds);
            var firstHalf = new List<List<(int, int)>>();

            for (int round = 0; round < n - 1; round++)
            {
                var pairs = new List<(int, int)>();
                for (int i = 0; i < n / 2; i++)
                {
                    int a = list[i];
                    int b = list[n - 1 - i];
                    pairs.Add(round % 2 == 1 && i == 0 ? (b, a) : (a, b));
                }
                firstHalf.Add(pairs);

                int last = list[n - 1];
                list.RemoveAt(n - 1);
                list.Insert(1, last);
            }

            var secondHalf = firstHalf.Select(r => r.Select(m => (m.Item2, m.Item1)).ToList()).ToList();
            var all = new List<List<(int, int)>>();
            all.AddRange(firstHalf);
            all.AddRange(secondHalf);
            return all;
        }

        public static List<StandingRow> CalculateGroupTable(
            IReadOnlyList<CupTie> groupTies, IReadOnlyDictionary<int, string> teamNames)
        {
            var acc = new Dictionary<int, (int Played, int Wins, int Draws, int Losses, int GoalsFor, int GoalsAgainst)>();

            void Ensure(int teamId) => acc.TryAdd(teamId, (0, 0, 0, 0, 0, 0));

            foreach (var t in groupTies)
            {
                Ensure(t.HomeTeamId);
                Ensure(t.AwayTeamId);
            }

            foreach (var t in groupTies.Where(t => t.Played))
            {
                var home = acc[t.HomeTeamId];
                var away = acc[t.AwayTeamId];
                home.Played++; away.Played++;
                home.GoalsFor += t.HomeGoals; home.GoalsAgainst += t.AwayGoals;
                away.GoalsFor += t.AwayGoals; away.GoalsAgainst += t.HomeGoals;

                if (t.HomeGoals > t.AwayGoals) { home.Wins++; away.Losses++; }
                else if (t.HomeGoals < t.AwayGoals) { away.Wins++; home.Losses++; }
                else { home.Draws++; away.Draws++; }

                acc[t.HomeTeamId] = home;
                acc[t.AwayTeamId] = away;
            }

            return acc
                .Select(kv => (kv.Key, kv.Value, Points: kv.Value.Wins * 3 + kv.Value.Draws))
                .OrderByDescending(x => x.Points)
                .ThenByDescending(x => x.Value.GoalsFor - x.Value.GoalsAgainst)
                .ThenByDescending(x => x.Value.GoalsFor)
                .Select((x, index) => new StandingRow(
                    index + 1, x.Key, teamNames.GetValueOrDefault(x.Key, $"Team {x.Key}"),
                    x.Value.Played, x.Value.Wins, x.Value.Draws, x.Value.Losses,
                    x.Value.GoalsFor, x.Value.GoalsAgainst, x.Value.GoalsFor - x.Value.GoalsAgainst,
                    x.Points, string.Empty))
                .ToList();
        }

        // Transition into the knockout bracket: group winners + runners-up (16 teams) - winners
        // and runners-up are drawn separately, no team meets a team from its own group in the
        // round of 16. The CL/Europa Cup round of 16 is always home/away, so two dates are
        // needed, both mandatory (not optional).
        public static List<CupTie> BuildRoundOfSixteen(
            Dictionary<string, List<StandingRow>> finalGroupTables, CompetitionType competition, int season,
            DateTime firstLegDate, DateTime secondLegDate, Random? random = null)
        {
            var rng = random ?? Random.Shared;
            var winners = finalGroupTables.Select(kv => (Group: kv.Key, TeamId: kv.Value[0].TeamId)).ToList();
            var runnersUp = finalGroupTables.Select(kv => (Group: kv.Key, TeamId: kv.Value[1].TeamId)).ToList();

            List<(string Group, int TeamId)>? pairing = null;
            for (int attempt = 0; attempt < 200 && pairing is null; attempt++)
            {
                var shuffledRunnersUp = runnersUp.OrderBy(_ => rng.Next()).ToList();
                bool valid = winners.Zip(shuffledRunnersUp, (w, r) => w.Group != r.Group).All(ok => ok);
                if (valid)
                    pairing = shuffledRunnersUp;
            }
            pairing ??= runnersUp; // fallback after 200 attempts (practically never needed with 8 groups)

            var ties = new List<CupTie>();
            for (int i = 0; i < winners.Count; i++)
            {
                int matchNumber = i + 1;
                ties.Add(new CupTie
                {
                    CompetitionType = competition,
                    Season = season,
                    Round = CupDrawService.RoundLastSixteen,
                    MatchNumberInRound = matchNumber,
                    HomeTeamId = winners[i].TeamId,
                    AwayTeamId = pairing[i].TeamId,
                    Date = firstLegDate,
                    LegNumber = CupTie.LegFirst,
                });
                ties.Add(new CupTie
                {
                    CompetitionType = competition,
                    Season = season,
                    Round = CupDrawService.RoundLastSixteen,
                    MatchNumberInRound = matchNumber,
                    HomeTeamId = pairing[i].TeamId,
                    AwayTeamId = winners[i].TeamId,
                    Date = secondLegDate,
                    LegNumber = CupTie.LegSecond,
                });
            }

            return ties;
        }
    }
}
