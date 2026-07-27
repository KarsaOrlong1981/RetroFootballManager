using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    // Regression test for the reported "only the goalkeeper is on the pitch, bench is
    // empty" bug: whatever the upstream cause, re-running LineupSelector.SelectLineup on a
    // squad with an incomplete starting XI must fully repair it to 11 starters + 9 bench.
    public class LineupSelfHealTests
    {
        [Fact]
        public void SelectLineup_RepairsASquadWithOnlyGoalkeeperStarting()
        {
            var players = PlayerGenerator.GenerateSquad(Nationality.Germany, 60, squadSize: 25, random: new Random(1));
            int id = 1;
            foreach (var p in players)
                p.Id = id++;

            var team = new Team { Statistics = new TeamStats() };
            team.Players.AddRange(players);

            // Simulate the reported broken state: only the keeper is InStartingXI, nobody
            // else has been promoted to OnBench (everyone else sits at the generator default
            // PlayerStatus.Available).
            foreach (var p in team.Players)
                p.Status = PlayerStatus.Available;
            var keeper = team.Players.First(p => p.Position == Position.Goalkeeper);
            keeper.Status = PlayerStatus.InStartingXI;

            Assert.Equal(1, team.Players.Count(p => p.Status == PlayerStatus.InStartingXI));
            Assert.Equal(0, team.Players.Count(p => p.Status == PlayerStatus.OnBench));

            LineupSelector.SelectLineup(team, FormationCatalog.F442);

            Assert.Equal(11, team.Players.Count(p => p.Status == PlayerStatus.InStartingXI));
            Assert.Equal(9, team.Players.Count(p => p.Status == PlayerStatus.OnBench));
            Assert.Contains(team.Players, p => p.EffectivePosition == Position.Goalkeeper && p.Status == PlayerStatus.InStartingXI);
        }
    }
}
