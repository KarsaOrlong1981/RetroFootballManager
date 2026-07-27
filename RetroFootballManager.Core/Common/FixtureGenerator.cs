using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    /// <summary>
    /// Generates the complete fixture schedule of a league: round-robin format with
    /// each team playing every other team once in the first round and once
    /// in the second round. Home/Away assignments alternate as much as
    ///possible; with an even number of teams, it is technically unavoidable that
    ///some teams will have two consecutive home or away matches (as in real leagues)
    /// this is accepted.
    /// </summary>
    public static class FixtureGenerator
    {
        // Generates all fixtures of a league. teamIds must contain an even number
        // of teams. firstMatchdaySaturday = the Saturday of matchday 1;
        // each subsequent matchday takes place one week later. For every matchday,
        // 5 matches are played on Saturday, the remaining ones on Sunday.
        //
        // Winter break: a gap in real calendar weeks is inserted between the last
        // matchday of the first round and the first matchday of the second round.
        // This ensures that the transfer window has an
        // actual calendar period to reopen, instead of the league running in one
        // continuous weekly cadence throughout the entire season.
        public const int WinterBreakWeeks = 4;

        public static List<Fixture> GenerateLeagueFixtures(
            IReadOnlyList<int> teamIds,
            int season,
            int leagueTier,
            DateTime firstMatchdaySaturday)
        {
            if (teamIds.Count < 2 || teamIds.Count % 2 != 0)
                throw new ArgumentException("teamIds muss eine gerade Anzahl >= 2 enthalten.", nameof(teamIds));

            var firstHalf = BuildRounds(teamIds);

            // Second round 
            var secondHalf = firstHalf
                .Select(round => round.Select(m => (Home: m.Away, Away: m.Home)).ToList())
                .ToList();

            var allRounds = new List<List<(int Home, int Away)>>();
            allRounds.AddRange(firstHalf);
            allRounds.AddRange(secondHalf);

            var fixtures = new List<Fixture>();
            for (int roundIndex = 0; roundIndex < allRounds.Count; roundIndex++)
            {
                int matchday = roundIndex + 1;
                int extraWeeks = roundIndex >= firstHalf.Count ? WinterBreakWeeks : 0;
                var saturday = firstMatchdaySaturday.AddDays((roundIndex + extraWeeks) * 7);
                var sunday = saturday.AddDays(1);

                var matches = allRounds[roundIndex];
                for (int i = 0; i < matches.Count; i++)
                {
                    var (home, away) = matches[i];
                    fixtures.Add(new Fixture
                    {
                        Season = season,
                        LeagueTier = leagueTier,
                        Matchday = matchday,
                        // First 5 matches on Saturday, all other matches on Sunday.
                        Date = i < 5 ? saturday : sunday,
                        HomeTeamId = home,
                        AwayTeamId = away,
                        Played = false,
                    });
                }
            }

            return fixtures;
        }

        // Round rubin
        private static List<List<(int Home, int Away)>> BuildRounds(IReadOnlyList<int> teamIds)
        {
            int n = teamIds.Count;
            var list = teamIds.ToList();
            var rounds = new List<List<(int Home, int Away)>>();

            for (int round = 0; round < n - 1; round++)
            {
                var pairs = new List<(int Home, int Away)>();
                for (int i = 0; i < n / 2; i++)
                {
                    int a = list[i];
                    int b = list[n - 1 - i];

                    // Home/Away alternation: in odd rounds, all pairings are flipped;
                    // otherwise, non‑pivot teams keep the same home/away side across
                    // multiple rounds.
                    if (round % 2 == 1)
                        pairs.Add((b, a));
                    else
                        pairs.Add((a, b));
                }
                rounds.Add(pairs);

                // Rotation: first Element fix, forward last to first position.
                int last = list[n - 1];
                list.RemoveAt(n - 1);
                list.Insert(1, last);
            }

            return rounds;
        }

        // get first Saturday for date.
        public static DateTime FirstSaturdayOnOrAfter(DateTime date)
        {
            int offset = ((int)DayOfWeek.Saturday - (int)date.DayOfWeek + 7) % 7;
            return date.Date.AddDays(offset);
        }
    }
}
