using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Converts a single match's PlayerStats into a school-grade rating: 1.0 (best) - 6.0
    // (worst). Pure, side-effect-free - directly unit-testable without a full match simulation.
    public static class MatchRatingCalculator
    {
        private const double BaseRating = 3.5;
        private const int MinPassSample = 5;
        private const int MinDuelSample = 2;
        private const int ReferenceMinutes = 60;

        public static double Calculate(PlayerStats matchStats, Position position, int minutesPlayed)
        {
            double rating = BaseRating;

            rating -= GoalBonus(position) * matchStats.Goals;
            rating -= 0.3 * matchStats.Assists;
            rating += 0.2 * matchStats.Offsides;

            if (matchStats.Passes >= MinPassSample)
            {
                double passRate = (double)matchStats.SuccessfulPasses / matchStats.Passes;
                rating -= (passRate - 0.75) * 1.0;
            }
            if (matchStats.Crosses >= MinPassSample)
            {
                double crossRate = (double)matchStats.SuccessfulCrosses / matchStats.Crosses;
                rating -= (crossRate - 0.35) * 0.6;
            }

            if (matchStats.Tackles >= MinDuelSample)
            {
                double tackleRate = (double)matchStats.TacklesWon / matchStats.Tackles;
                rating -= (tackleRate - 0.5) * 0.8;
            }
            if (matchStats.Dribbles >= MinDuelSample)
            {
                double dribbleRate = (double)matchStats.SuccessfulDribbles / matchStats.Dribbles;
                rating -= (dribbleRate - 0.5) * 0.8;
            }
            if (matchStats.HeaderDuels >= MinDuelSample)
            {
                double headerRate = (double)matchStats.HeaderDuelsWon / matchStats.HeaderDuels;
                rating -= (headerRate - 0.5) * 0.6;
            }

            if (position == Position.Goalkeeper)
            {
                rating -= matchStats.Saves * 0.15;
                rating += matchStats.GoalsConceded * 0.3;
                if (matchStats.GoalsConceded == 0 && minutesPlayed >= ReferenceMinutes)
                    rating -= 0.3;
            }

            rating += matchStats.Fouls * 0.05;
            rating += matchStats.YellowCards * 0.5;
            rating += matchStats.RedCards * 1.5;

            // Dampens short cameo appearances toward the base rating instead of letting a
            // single event swing the grade to the extreme - minutesPlayed is never used as a
            // divisor, so this is safe even for a live/preview rating at minute 0.
            double dampingFactor = Math.Clamp((double)minutesPlayed / ReferenceMinutes, 0.15, 1.0);
            rating = BaseRating + ((rating - BaseRating) * dampingFactor);

            return Math.Clamp(rating, 1.0, 6.0);
        }

        // Goals are weighted least for forwards (expected of them) and most for defenders/
        // goalkeepers (a rare, high-impact event for their position).
        private static double GoalBonus(Position position) => position switch
        {
            Position.Goalkeeper => 1.6,
            Position.CentralDefender or Position.LeftDefender or Position.RightDefender => 1.4,
            Position.LeftWingBack or Position.RightWingBack => 1.3,
            Position.DefensiveMidfielder or Position.CentralMidfielder
                or Position.LeftMidfielder or Position.RightMidfielder => 1.1,
            Position.CentralOffenseMidfielder or Position.LeftOffenseMidfielder or Position.RightOffenseMidfielder => 1.0,
            Position.Forward => 0.8,
            _ => 1.0,
        };
    }
}
