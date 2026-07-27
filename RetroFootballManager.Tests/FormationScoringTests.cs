using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class FormationScoringTests
    {
        // Uniform-attribute player: scoring is attribute/position-weighted (PlayerRoleRating), not
        // a flat Rating, so every attribute needs a real value, not just Rating.
        private static Player MakeUniform(int id, Position position, int level) => new()
        {
            Id = id,
            Position = position,
            Rating = level,
            OffensivePower = level, DefensivePower = level, GameIntelligence = level,
            PressingIntensity = level, CounterSpeed = level, PassingAccuracy = level,
            DuelHardness = level, DuelEfficiency = level, CrossingAccuracy = level,
            GkReflexes = level, GkHandling = level, GkOneOnOne = level,
            GkDistribution = level, GkAerialControl = level,
            HeaderStrength = level, Jumping = level, Dribbling = level,
            LongShotAccuracy = level, PenaltyKick = level, FreeKick = level,
            Finishing = level, Positioning = level,
        };

        [Fact]
        public void ScoreFormation_PrefersFormationThatFitsSquadBetter()
        {
            // A squad packed with strikers should score higher for a two-striker formation
            // than for a lone-striker one.
            var team = new Team { Statistics = new TeamStats() };
            int id = 1;

            team.Players.Add(MakeUniform(id++, Position.Goalkeeper, 60));

            for (int i = 0; i < 4; i++)
                team.Players.Add(MakeUniform(id++, Position.CentralDefender, 60));
            for (int i = 0; i < 4; i++)
                team.Players.Add(MakeUniform(id++, Position.CentralMidfielder, 60));
            // Four strikers - more than any single-forward formation needs.
            for (int i = 0; i < 4; i++)
                team.Players.Add(MakeUniform(id++, Position.Forward, 75));

            double f442Score = LineupSelector.ScoreFormation(team, FormationCatalog.F442); // 2 forwards
            double f4231Score = LineupSelector.ScoreFormation(team, FormationCatalog.F4231); // 1 forward

            Assert.True(f442Score > f4231Score);
        }
    }
}
