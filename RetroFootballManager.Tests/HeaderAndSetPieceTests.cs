using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class HeaderAndSetPieceTests
    {
        [Fact]
        public void HeaderGoalProbability_StrongAerialShooter_BeatsWeakAerialShooter()
        {
            // A full match simulation drowns this signal in open-play noise (see
            // Match.HeaderGoalProbability's own comment) - test the header duel math directly
            // and deterministically instead, same pattern as
            // PenaltyConversionProbability_StrongTaker_BeatsWeakTaker_AgainstTheSameKeeper below.
            const double crossQuality = 0.85;
            const double defenderHeaderPower = 65;
            const double keeperAerialControl = 65;
            const double buildUpRatio = 0.5;

            double strongProb = Match.HeaderGoalProbability(
                shooterHeaderPower: 95, crossQuality, defenderHeaderPower, keeperAerialControl, buildUpRatio);
            double weakProb = Match.HeaderGoalProbability(
                shooterHeaderPower: 20, crossQuality, defenderHeaderPower, keeperAerialControl, buildUpRatio);

            Assert.True(strongProb > weakProb, $"strong={strongProb}, weak={weakProb}");
        }

        [Fact]
        public void Simulate_GoodCrosser_BoostsHeaderGoals_ForwardHeaderAbilityHeldConstant()
        {
            var random = new Random(53);
            int goalsGoodCrosser = 0;
            int goalsPoorCrosser = 0;
            const int matches = 200;

            for (int i = 0; i < matches; i++)
            {
                var goodCrosserTeam = TestHelpers.CreateTeam("GuterFlankengeber", baseRating: 65);
                foreach (var p in goodCrosserTeam.Players.Where(p => p.Position == Position.Forward))
                {
                    p.HeaderStrength = 80;
                    p.Jumping = 80;
                    p.Size = 1.9;
                }
                foreach (var p in goodCrosserTeam.Players.Where(p => p.Position is Position.LeftMidfielder or Position.RightMidfielder))
                    p.CrossingAccuracy = 95;
                var defenders1 = TestHelpers.CreateTeam("Abwehr1", baseRating: 65);
                goalsGoodCrosser += new Match(goodCrosserTeam, defenders1, random).Simulate().HomeGoals;

                var poorCrosserTeam = TestHelpers.CreateTeam("SchlechterFlankengeber", baseRating: 65);
                foreach (var p in poorCrosserTeam.Players.Where(p => p.Position == Position.Forward))
                {
                    p.HeaderStrength = 80;
                    p.Jumping = 80;
                    p.Size = 1.9;
                }
                foreach (var p in poorCrosserTeam.Players.Where(p => p.Position is Position.LeftMidfielder or Position.RightMidfielder))
                    p.CrossingAccuracy = 15;
                var defenders2 = TestHelpers.CreateTeam("Abwehr2", baseRating: 65);
                goalsPoorCrosser += new Match(poorCrosserTeam, defenders2, random).Simulate().HomeGoals;
            }

            Assert.True(goalsGoodCrosser > goalsPoorCrosser,
                $"good crosser={goalsGoodCrosser}, poor crosser={goalsPoorCrosser}");
        }

        [Fact]
        public void PenaltyConversionProbability_StrongTaker_BeatsWeakTaker_AgainstTheSameKeeper()
        {
            // A full match simulation drowns this signal in open-play noise (penalties are rare,
            // ~0.2/match, versus ~1.7 open-play goals/match) - test the taker-vs-keeper duel math
            // directly and deterministically instead (see Match.PenaltyConversionProbability).
            const double keeperReflexes = (65 * 0.5) + (65 * 0.3) + (65 * 0.2); // = 65

            double strongProb = Match.PenaltyConversionProbability(takerComposure: 95, keeperReflexes);
            double weakProb = Match.PenaltyConversionProbability(takerComposure: 15, keeperReflexes);

            Assert.True(strongProb > weakProb, $"strong={strongProb}, weak={weakProb}");
        }
    }
}
