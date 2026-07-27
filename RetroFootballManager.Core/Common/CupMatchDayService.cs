using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Plays out a complete cup round: the manager's match (if involved in this round) is
    // played live in the UI (result is passed in), all other matches are simulated
    // immediately (AI on both sides) - analogous to MatchDayService.PlayMatchdayAsync,
    // but without league assumptions (standings points/form stay untouched) and with
    // penalty-shootout resolution on a draw.
    public class CupMatchDayService
    {
        private readonly CupTieRepository _cupTies;
        private readonly TeamRepository _teams;
        private readonly MessageService? _messages;
        private readonly PlayerRepository? _players;
        private readonly Random _random;

        public CupMatchDayService(
            CupTieRepository cupTies, TeamRepository teams, Random? random = null, MessageService? messages = null,
            PlayerRepository? players = null)
        {
            _cupTies = cupTies;
            _teams = teams;
            _random = random ?? Random.Shared;
            _messages = messages;
            _players = players;
        }

        public async Task<List<CupTie>> PlayCupRoundAsync(
            IReadOnlyList<Team> teams, IReadOnlyList<CupTie> roundTies, CupTie? humanTie, MatchResult? humanResult,
            int humanTeamId = 0, DateTime? currentDate = null, IReadOnlyList<CupTie>? firstLegTies = null)
        {
            var teamById = teams.ToDictionary(t => t.Id);
            var touchedTeamIds = new HashSet<int>();

            foreach (var tie in roundTies)
            {
                // Bye: already marked played/resolved when drawn (see CupDrawService.NewByeTie) -
                // AwayTeamId=0 doesn't exist in teamById, so there's nothing to simulate/save.
                if (tie.IsBye)
                    continue;

                var home = teamById[tie.HomeTeamId];
                var away = teamById[tie.AwayTeamId];
                bool isFinal = tie.Round == CupDrawService.RoundFinal;
                var originalHomeStadium = home.Stadium;

                try
                {
                    // Final: neutral stadium with no home advantage for the duration of the match.
                    if (isFinal)
                        home.Stadium = CupDrawService.CreateFinalStadium();

                    MatchResult result;
                    if (humanTie is not null && tie.Id == humanTie.Id && humanResult is not null)
                    {
                        result = humanResult;
                    }
                    else
                    {
                        MatchDayService.PrepareForMatch(home, currentDate);
                        MatchDayService.PrepareForMatch(away, currentDate);

                        var match = new Match(home, away, _random)
                        {
                            HomeCoach = new AiMatchCoach(),
                            AwayCoach = new AiMatchCoach(),
                        };
                        result = match.Simulate();
                    }

                    tie.HomeGoals = result.HomeGoals;
                    tie.AwayGoals = result.AwayGoals;
                    tie.Played = true;

                    var pairedFirstLeg = tie.LegNumber == CupTie.LegSecond
                        ? firstLegTies?.FirstOrDefault(t => t.MatchNumberInRound == tie.MatchNumberInRound)
                        : null;

                    if (CupTieHelper.RequiresPenaltyShootout(tie, pairedFirstLeg))
                    {
                        var (penaltyHome, penaltyAway) = ResolvePenaltyShootout(home, away, _random);
                        tie.WentToPenalties = true;
                        tie.PenaltyHomeGoals = penaltyHome;
                        tie.PenaltyAwayGoals = penaltyAway;
                    }

                    result.ApplyInjuryDurations(tie.Date);
                    if (_messages is not null && humanTeamId != 0)
                        await MatchDayService.NotifyInjuriesAsync(_messages, result, home, away, humanTeamId, tie.Date);

                    MatchDayService.ApplyCareerMinutes(result, home, away);
                    if (_players is not null)
                        await MatchDayService.PersistPlayerStatsAsync(_players, [result], tie.Season, tie.CompetitionType);
                }
                finally
                {
                    home.Stadium = originalHomeStadium;
                }

                touchedTeamIds.Add(home.Id);
                touchedTeamIds.Add(away.Id);
            }

            foreach (var tie in roundTies)
                await _cupTies.SaveAsync(tie);
            foreach (var id in touchedTeamIds)
                await _teams.SaveTeamAsync(teamById[id], includeYouth: false);

            return roundTies.ToList();
        }

        // Regular 5 rounds, then sudden death - simplified (no early stop when one side can no
        // longer mathematically catch up). Taker order descending by PenaltyKick; duel formula
        // analogous to Match.ResolvePenalty (in-game penalty).
        public static (int Home, int Away) ResolvePenaltyShootout(Team home, Team away, Random rng)
        {
            var homeTakers = TeamStrengthCalculator.GetLineup(home).OrderByDescending(p => p.PenaltyKick).ToList();
            var awayTakers = TeamStrengthCalculator.GetLineup(away).OrderByDescending(p => p.PenaltyKick).ToList();
            var homeKeeper = TeamStrengthCalculator.GetGoalkeeper(home);
            var awayKeeper = TeamStrengthCalculator.GetGoalkeeper(away);

            int homeGoals = 0, awayGoals = 0;
            for (int round = 0; round < 5; round++)
            {
                if (TakeShot(homeTakers, round, awayKeeper, rng)) homeGoals++;
                if (TakeShot(awayTakers, round, homeKeeper, rng)) awayGoals++;
            }

            for (int round = 5; homeGoals == awayGoals; round++)
            {
                if (TakeShot(homeTakers, round, awayKeeper, rng)) homeGoals++;
                if (TakeShot(awayTakers, round, homeKeeper, rng)) awayGoals++;
            }

            return (homeGoals, awayGoals);
        }

        private static bool TakeShot(IReadOnlyList<Player> takers, int round, Player? keeper, Random rng)
        {
            double takerSkill = takers.Count > 0 ? takers[round % takers.Count].PenaltyKick : 50;
            double keeperSkill = keeper is not null
                ? (keeper.GkOneOnOne * 0.5) + (keeper.GkReflexes * 0.3) + (keeper.DuelEfficiency * 0.2)
                : 50;

            double duelRatio = takerSkill / Math.Max(0.001, takerSkill + keeperSkill * 0.8);
            double conversionProb = Math.Clamp(0.75 + (duelRatio - 0.55) * 0.4, 0.55, 0.95);
            return rng.NextDouble() < conversionProb;
        }
    }
}
