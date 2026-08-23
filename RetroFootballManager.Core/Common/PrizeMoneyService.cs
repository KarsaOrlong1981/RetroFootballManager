using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Season-end prize money: league placement (tiers 2-4, top 3), continental qualification
    // (league 1, positions 1-7) and cup prizes (round-reached fee, cumulative, plus a
    // final-placement bonus for the last 4). Paid on top of the existing sponsor bonuses
    // (see SaveGameService.PaySponsorSeasonBonusesAsync) - not a replacement for them.
    public static class PrizeMoneyService
    {
        private static readonly Dictionary<(int Tier, int Position), int> LeaguePlacementPrizes = new()
        {
            [(4, 1)] = 250_000, [(4, 2)] = 190_000, [(4, 3)] = 150_000,
            [(3, 1)] = 500_000, [(3, 2)] = 350_000, [(3, 3)] = 250_000,
            [(2, 1)] = 1_100_000, [(2, 2)] = 700_000, [(2, 3)] = 450_000,
        };

        public const int ChampionsLeagueQualificationPrize = 2_000_000;
        public const int EuropaCupQualificationPrize = 1_000_000;

        public static void AwardLeaguePrizes(IReadOnlyList<Team> teams, SeasonEndResult result)
        {
            var teamsById = teams.ToDictionary(t => t.Id);

            foreach (var league in result.Leagues)
            {
                foreach (var row in league.Table)
                {
                    if (!teamsById.TryGetValue(row.TeamId, out var team) || team.Finances is null)
                        continue;

                    int prize = league.Tier == 1
                        ? row.Position <= 4 ? ChampionsLeagueQualificationPrize
                            : row.Position <= 7 ? EuropaCupQualificationPrize
                            : 0
                        : LeaguePlacementPrizes.GetValueOrDefault((league.Tier, row.Position));

                    if (prize <= 0)
                        continue;

                    team.Finances.CurrentBalance += prize;
                    team.Finances.PrizeMoney += prize;
                }
            }
        }

        // Fee for reaching a round (cumulative - paid once per distinct round a team appears
        // in). Rounds not listed (e.g. German Cup's Preliminary, CL/EC's non-existent rounds
        // 1-3) are worth 0.
        private static readonly Dictionary<int, int> GermanCupRoundReachedPrize = new()
        {
            [CupDrawService.RoundLastSixtyFour] = 10_000,
            [CupDrawService.RoundLastThirtyTwo] = 20_000,
            [CupDrawService.RoundLastSixteen] = 40_000,
            [CupDrawService.RoundQuarterFinal] = 80_000,
            [CupDrawService.RoundSemiFinal] = 150_000,
            [CupDrawService.RoundFinal] = 300_000,
        };

        private static readonly Dictionary<int, int> ChampionsLeagueRoundReachedPrize = new()
        {
            [0] = 500_000, // group stage
            [CupDrawService.RoundLastSixteen] = 300_000,
            [CupDrawService.RoundQuarterFinal] = 600_000,
            [CupDrawService.RoundSemiFinal] = 1_000_000,
            [CupDrawService.RoundFinal] = 1_500_000,
        };

        private static readonly Dictionary<int, int> EuropaCupRoundReachedPrize = new()
        {
            [0] = 300_000, // group stage
            [CupDrawService.RoundLastSixteen] = 180_000,
            [CupDrawService.RoundQuarterFinal] = 350_000,
            [CupDrawService.RoundSemiFinal] = 600_000,
            [CupDrawService.RoundFinal] = 900_000,
        };

        private readonly record struct FinalPlacementPrize(int Winner, int Finalist, int SemiFinalOut);

        private static readonly Dictionary<CompetitionType, FinalPlacementPrize> FinalPlacementPrizes = new()
        {
            [CompetitionType.GermanCup] = new(2_000_000, 1_200_000, 500_000),
            [CompetitionType.ChampionsLeague] = new(5_000_000, 3_000_000, 2_000_000),
            [CompetitionType.EuropaCup] = new(3_200_000, 2_100_000, 1_550_000),
        };

        private static Dictionary<int, int> RoundReachedPrizes(CompetitionType competition) => competition switch
        {
            CompetitionType.GermanCup => GermanCupRoundReachedPrize,
            CompetitionType.ChampionsLeague => ChampionsLeagueRoundReachedPrize,
            CompetitionType.EuropaCup => EuropaCupRoundReachedPrize,
            _ => [],
        };

        // Derives, purely from this season's played CupTie rows, how far each team got and pays
        // the cumulative round-reached fees plus (winner/finalist/semifinal-out) a placement
        // bonus - analogous to how CupParticipationService derives status without any separate
        // elimination tracking. Teams whose furthest round isn't fully played yet are skipped
        // (no partial credit; they simply weren't resolved by season end).
        public static void AwardCupPrizes(IReadOnlyList<Team> teams, IReadOnlyList<CupTie> ties, CompetitionType competition)
        {
            if (ties.Count == 0 || !FinalPlacementPrizes.TryGetValue(competition, out var placement))
                return;

            var teamsById = teams.ToDictionary(t => t.Id);
            var roundPrizes = RoundReachedPrizes(competition);

            var teamIds = ties.SelectMany(t => t.IsBye ? new[] { t.HomeTeamId } : new[] { t.HomeTeamId, t.AwayTeamId }).Distinct();

            foreach (var teamId in teamIds)
            {
                if (!teamsById.TryGetValue(teamId, out var team) || team.Finances is null)
                    continue;

                var participated = ties.Where(t => t.HomeTeamId == teamId || (!t.IsBye && t.AwayTeamId == teamId)).ToList();
                if (participated.Count == 0)
                    continue;

                int maxRound = participated.Max(t => t.Round);
                var finalRoundTies = participated.Where(t => t.Round == maxRound).ToList();
                if (finalRoundTies.Any(t => !t.Played))
                    continue;

                int prize = participated.Select(t => t.Round).Distinct().Sum(round => roundPrizes.GetValueOrDefault(round));

                // Group-stage rows (round 0) never form a 1-or-2-row knockout pairing, so the
                // win/loss check only ever runs for genuine semifinal/final ties.
                if (maxRound == CupDrawService.RoundFinal || maxRound == CupDrawService.RoundSemiFinal)
                {
                    bool won = finalRoundTies.Any(t => t.IsBye) || CupTieHelper.DetermineAggregateWinner(finalRoundTies) == teamId;
                    if (maxRound == CupDrawService.RoundFinal)
                        prize += won ? placement.Winner : placement.Finalist;
                    else if (!won)
                        prize += placement.SemiFinalOut;
                }

                if (prize <= 0)
                    continue;

                team.Finances.CurrentBalance += prize;
                team.Finances.PrizeMoney += prize;
            }
        }
    }
}
