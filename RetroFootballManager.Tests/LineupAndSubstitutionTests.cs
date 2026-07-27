using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class LineupAndSubstitutionTests
    {
        // Full 25-man squad with unique ids and a position-correct lineup (11 + 9 bench).
        private static Team BuildMatchReadyTeam(string name, int seed, double rating = 60)
        {
            var players = PlayerGenerator.GenerateSquad(
                Nationality.Germany, rating, squadSize: 25, random: new Random(seed));
            int id = seed * 1000 + 1;
            foreach (var p in players)
            {
                p.Id = id++;
                p.Fitness = 100;
            }

            var team = new Team { Name = name, Statistics = new TeamStats() };
            team.Players.AddRange(players);
            LineupSelector.SelectLineup(team);
            return team;
        }

        [Fact]
        public void LineupSelector_PicksAGoalkeeperInGoal()
        {
            var team = BuildMatchReadyTeam("T", 1);
            var starters = team.Players.Where(p => p.Status == PlayerStatus.InStartingXI).ToList();

            Assert.Equal(11, starters.Count);
            var keeper = starters.Single(p => p.EffectivePosition == Position.Goalkeeper);
            Assert.Equal(Position.Goalkeeper, keeper.Position);
        }

        [Fact]
        public void LineupSelector_CapsBenchAtNine()
        {
            var team = BuildMatchReadyTeam("T", 2);
            Assert.Equal(9, team.Players.Count(p => p.Status == PlayerStatus.OnBench));
        }

        [Fact]
        public void Substitution_SwapsPlayersAndTracksCount()
        {
            var home = BuildMatchReadyTeam("Heim", 3);
            var away = BuildMatchReadyTeam("Gast", 4);
            var match = new Match(home, away, new Random(1));
            match.Begin();
            match.AdvanceMinute();

            var off = match.OnPitch(true).First(p => p.EffectivePosition != Position.Goalkeeper);
            var on = match.Bench(true).First();

            Assert.True(match.TrySubstitute(true, off, on));
            Assert.Equal(PlayerStatus.SubstitutedOff, off.Status);
            Assert.Equal(PlayerStatus.InStartingXI, on.Status);
            Assert.DoesNotContain(off, match.OnPitch(true));
            Assert.Contains(on, match.OnPitch(true));
            Assert.Equal(1, match.SubsUsed(true));
        }

        [Fact]
        public void Substitution_LimitedToFivePerTeam()
        {
            var home = BuildMatchReadyTeam("Heim", 5);
            var away = BuildMatchReadyTeam("Gast", 6);
            var match = new Match(home, away, new Random(1));
            match.Begin();

            var starters = match.OnPitch(true)
                .Where(p => p.EffectivePosition != Position.Goalkeeper).Take(6).ToList();
            var bench = match.Bench(true).Take(6).ToList();

            for (int i = 0; i < 5; i++)
                Assert.True(match.TrySubstitute(true, starters[i], bench[i]));

            // Sixth substitution must be rejected.
            Assert.False(match.TrySubstitute(true, starters[5], bench[5]));
            Assert.Equal(5, match.SubsUsed(true));
        }

        [Fact]
        public void Substitution_CannotBringOnAPlayerNotOnTheBench()
        {
            var home = BuildMatchReadyTeam("Heim", 7);
            var away = BuildMatchReadyTeam("Gast", 8);
            var match = new Match(home, away, new Random(1));
            match.Begin();

            var starterA = match.OnPitch(true).First(p => p.EffectivePosition != Position.Goalkeeper);
            var starterB = match.OnPitch(true).Last();

            // starterB is on the pitch, not on the bench → invalid.
            Assert.False(match.TrySubstitute(true, starterA, starterB));
        }
    }
}
