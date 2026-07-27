using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Central place for "is this round home/away", "who advances on aggregate",
    // "does this result go to a penalty shootout" - reused by CupDrawService,
    // CupMatchDayService, the ViewModel, and the cup overview.
    public static class CupTieHelper
    {
        // Only round of 16/quarterfinal/semifinal of CL/EC - never German Cup,
        // never the group stage (Round 0), never the final (neutral venue, single match).
        public static bool IsTwoLegged(CompetitionType competition, int round) =>
            competition != CompetitionType.GermanCup
            && round is CupDrawService.RoundLastSixteen or CupDrawService.RoundQuarterFinal
                        or CupDrawService.RoundSemiFinal;

        // pairingTies: 1 row (regular knockout match/final) or 2 rows (home/away leg, any
        // order).
        public static int DetermineAggregateWinner(IReadOnlyList<CupTie> pairingTies)
        {
            if (pairingTies.Count == 1)
                return pairingTies[0].WinnerTeamId;

            var leg1 = pairingTies.First(t => t.LegNumber == CupTie.LegFirst);
            var leg2 = pairingTies.First(t => t.LegNumber == CupTie.LegSecond);
            int teamA = leg1.HomeTeamId;  // same as leg2.AwayTeamId
            int teamB = leg1.AwayTeamId;  // same as leg2.HomeTeamId

            int aggA = leg1.HomeGoals + leg2.AwayGoals;
            int aggB = leg1.AwayGoals + leg2.HomeGoals;
            if (aggA != aggB)
                return aggA > aggB ? teamA : teamB;

            return leg2.WentToPenalties && leg2.PenaltyHomeGoals > leg2.PenaltyAwayGoals ? teamB : teamA;
        }

        public static bool IsAggregateTied(CupTie leg1, CupTie leg2) =>
            (leg1.HomeGoals + leg2.AwayGoals) == (leg1.AwayGoals + leg2.HomeGoals);

        // Group stage: never. First leg: never (the second leg decides). Second leg: only if
        // the AGGREGATE after 90 minutes of leg 2 is tied (not the second-leg result itself!).
        // Everything else (German Cup any round, CL/EC final): classic 90-minute tie rule.
        public static bool RequiresPenaltyShootout(CupTie tie, CupTie? firstLeg = null)
        {
            if (tie.Round == 0 || tie.LegNumber == CupTie.LegFirst)
                return false;
            if (tie.LegNumber == CupTie.LegSecond)
                return firstLeg is not null && IsAggregateTied(firstLeg, tie);
            return tie.HomeGoals == tie.AwayGoals;
        }
    }
}
