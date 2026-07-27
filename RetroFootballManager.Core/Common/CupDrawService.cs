using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Builds the draw/round tree of the German Cup (and provides the generic round-progression
    // logic that M6c/M6d reuse for the KO stage after the group stage). Round progression is
    // purely arithmetic: round R with K ties -> round R+1 has K/2 ties, tie i = winner(tie 2i-1)
    // vs winner(tie 2i) - no "NextMatchNumber" column needed.
    public static class CupDrawService
    {
        public const int RoundPreliminary = 1;    // preliminary round
        public const int RoundLastSixtyFour = 2;
        public const int RoundLastThirtyTwo = 3;  // round of 32
        public const int RoundLastSixteen = 4;    // round of 16
        public const int RoundQuarterFinal = 5;
        public const int RoundSemiFinal = 6;
        public const int RoundFinal = 7;

        // From the round of 16 (round 4) onward, home advantage no longer depends on league strength.
        private const int LastTierDependentRound = RoundLastThirtyTwo;

        // Round 1: ALL teams from all 4 leagues take part, randomly paired - no hidden preliminary
        // round/byes anymore (LeagueTier >= 1 excludes fictional CL/Europa Cup clubs, which have
        // LeagueTier 0 and never play in the German Cup).
        public static List<CupTie> BuildGermanCupFirstRound(
            IReadOnlyList<Team> teams, int season, DateTime date, Random? random = null)
        {
            var participants = teams.Where(t => t.LeagueTier >= 1).ToList();
            if (participants.Count < 2)
                throw new ArgumentException("Erwartet mindestens 2 Teams.", nameof(teams));

            var rng = random ?? Random.Shared;
            var shuffled = participants.OrderBy(_ => rng.Next()).ToList();

            var ties = new List<CupTie>();
            for (int i = 0; i + 1 < shuffled.Count; i += 2)
            {
                var (home, away) = AssignHomeAdvantage(shuffled[i], shuffled[i + 1], RoundPreliminary, rng);
                ties.Add(NewTie(CompetitionType.GermanCup, season, RoundPreliminary, i / 2 + 1, home.Id, away.Id, date));
            }

            // Odd team count: one random team gets a bye straight into round 2.
            if (shuffled.Count % 2 != 0)
            {
                var byeTeam = shuffled[^1];
                ties.Add(NewByeTie(CompetitionType.GermanCup, season, RoundPreliminary, ties.Count + 1, byeTeam.Id, date));
            }

            return ties;
        }

        // From round 3 (round of 32) onward: pure winner pairing, no more byes. Pairing is
        // grouped by MatchNumberInRound (works for both single-row and two-leg preliminary
        // rounds) - the winner is determined via CupTieHelper.DetermineAggregateWinner
        // (aggregate on two legs, otherwise the simple match winner).
        public static List<CupTie> BuildNextRound(
            IReadOnlyList<CupTie> previousRoundTies, IReadOnlyDictionary<int, Team> teamsById, int season,
            DateTime date, Random? random = null, DateTime? secondLegDate = null)
        {
            if (previousRoundTies.Count == 0)
                return [];
            if (previousRoundTies.Any(t => !t.Played))
                throw new InvalidOperationException("Alle Partien der Runde müssen gespielt sein.");

            var pairings = previousRoundTies
                .GroupBy(t => t.MatchNumberInRound)
                .OrderBy(g => g.Key)
                .Select(g => CupTieHelper.DetermineAggregateWinner(g.ToList()))
                .ToList();

            int nextRound = previousRoundTies.Max(t => t.Round) + 1;
            var competition = previousRoundTies[0].CompetitionType;
            var rng = random ?? Random.Shared;
            bool twoLegged = CupTieHelper.IsTwoLegged(competition, nextRound);

            // Odd number of winners (e.g. 72-team bracket: 72->36->18->9->5->3->2->1, three
            // odd intermediate steps): one random winner gets a bye into the next round
            // instead of being paired.
            int? byeTeamId = null;
            if (pairings.Count % 2 != 0)
            {
                int byeIndex = rng.Next(pairings.Count);
                byeTeamId = pairings[byeIndex];
                pairings.RemoveAt(byeIndex);
            }

            var ties = new List<CupTie>();
            for (int i = 0; i < pairings.Count; i += 2)
            {
                var home = teamsById[pairings[i]];
                var away = teamsById[pairings[i + 1]];
                int matchNumber = i / 2 + 1;

                if (twoLegged)
                {
                    if (secondLegDate is null)
                        throw new ArgumentException("Hin-/Rückspiel benötigt zwei Termine.", nameof(secondLegDate));

                    ties.Add(NewTie(competition, season, nextRound, matchNumber, home.Id, away.Id, date, CupTie.LegFirst));
                    ties.Add(NewTie(competition, season, nextRound, matchNumber, away.Id, home.Id, secondLegDate.Value, CupTie.LegSecond));
                }
                else
                {
                    var (h, a) = AssignHomeAdvantage(home, away, nextRound, rng);
                    ties.Add(NewTie(competition, season, nextRound, matchNumber, h.Id, a.Id, date));
                }
            }

            if (byeTeamId is int byeId)
                ties.Add(NewByeTie(competition, season, nextRound, ties.Count + 1, byeId, date));

            return ties;
        }

        private static (Team Home, Team Away) AssignHomeAdvantage(Team a, Team b, int round, Random rng)
        {
            if (round > LastTierDependentRound || a.LeagueTier == b.LeagueTier)
                return rng.Next(2) == 0 ? (a, b) : (b, a);

            // The team from the weaker/lower league (higher tier number) gets home advantage.
            return a.LeagueTier > b.LeagueTier ? (a, b) : (b, a);
        }

        private static CupTie NewTie(
            CompetitionType competition, int season, int round, int matchNumber, int homeTeamId, int awayTeamId,
            DateTime date, int legNumber = CupTie.LegNone) => new()
            {
                CompetitionType = competition,
                Season = season,
                Round = round,
                MatchNumberInRound = matchNumber,
                HomeTeamId = homeTeamId,
                AwayTeamId = awayTeamId,
                Date = date,
                LegNumber = legNumber,
            };

        // Bye: already "played" with a sentinel result, so WinnerTeamId resolves to teamId
        // right away and the existing round-completion logic keeps working unchanged -
        // AwayTeamId=0 is never a real team ID, IsBye is the actual marker.
        private static CupTie NewByeTie(
            CompetitionType competition, int season, int round, int matchNumber, int teamId, DateTime date) => new()
            {
                CompetitionType = competition,
                Season = season,
                Round = round,
                MatchNumberInRound = matchNumber,
                HomeTeamId = teamId,
                AwayTeamId = 0,
                Date = date,
                Played = true,
                HomeGoals = 1,
                AwayGoals = 0,
                IsBye = true,
            };

        // Neutral final stadium ("Olympiastadion Berlin") - no team binding (TeamId stays
        // unused), HomeAdvantage=0 gives no bonus to either side. Not persisted, but created
        // fresh when needed and passed to Match for the duration of the final.
        public static Stadium CreateFinalStadium() => new()
        {
            Name = "Olympiastadion Berlin",
            SeatingCapacity = 55_000,
            StandingCapacity = 15_000,
            LogeCapacity = 4_000,
            Atmosphere = 95,
            Condition = 95,
            HomeAdvantage = 0,
            HasRoof = false,
        };
    }
}
