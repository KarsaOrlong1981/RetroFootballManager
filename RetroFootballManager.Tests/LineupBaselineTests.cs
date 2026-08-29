using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    // Covers the persistent-lineup fixes: a squad departure only ever refills the one vacated
    // slot (never reshuffles the rest of the manager's bench), and RestoreBaseline undoes
    // in-match subs/reshuffles for the next matchday while leaving an injury/suspension alone.
    public class LineupBaselineTests
    {
        private static Team BuildFullSquadWithBaseline()
        {
            var players = PlayerGenerator.GenerateSquad(Nationality.Germany, 60, squadSize: 25, random: new Random(4));
            int id = 1;
            foreach (var p in players)
                p.Id = id++;

            var team = new Team { Statistics = new TeamStats() };
            team.Players.AddRange(players);
            LineupSelector.SelectLineup(team, FormationCatalog.F442);

            team.BaselineStartingIds = team.Players.Where(p => p.Status == PlayerStatus.InStartingXI).Select(p => p.Id).ToList();
            team.BaselineBenchIds = team.Players.Where(p => p.Status == PlayerStatus.OnBench).Select(p => p.Id).ToList();
            return team;
        }

        [Fact]
        public void RefillBench_WhenABenchPlayerLeaves_OnlyReplacesThatOneSlot()
        {
            var team = BuildFullSquadWithBaseline();
            var starterIdsBefore = team.BaselineStartingIds.ToList();
            var benchIdsBefore = team.BaselineBenchIds.ToList();

            var departed = team.Players.First(p => p.Id == benchIdsBefore[0]);
            team.Players.Remove(departed);

            LineupSelector.RefillBench(team);

            Assert.Equal(starterIdsBefore, team.BaselineStartingIds);
            Assert.Equal(9, team.BaselineBenchIds.Count);
            // Every OTHER original bench player is still exactly where they were.
            foreach (var id in benchIdsBefore.Skip(1))
                Assert.Contains(id, team.BaselineBenchIds);
            Assert.DoesNotContain(departed.Id, team.BaselineBenchIds);

            var newcomer = team.Players.First(p => team.BaselineBenchIds.Contains(p.Id) && !benchIdsBefore.Contains(p.Id));
            Assert.Equal(PlayerStatus.OnBench, newcomer.Status);
        }

        [Fact]
        public void RefillBench_WhenAStarterLeaves_PromotesAReplacementIntoThatSlotOnly()
        {
            var team = BuildFullSquadWithBaseline();
            var starterIdsBefore = team.BaselineStartingIds.ToList();
            var benchIdsBefore = team.BaselineBenchIds.ToList();

            var departed = team.Players.First(p => p.Id == starterIdsBefore[0]);
            team.Players.Remove(departed);

            LineupSelector.RefillBench(team);

            Assert.Equal(11, team.BaselineStartingIds.Count);
            Assert.DoesNotContain(departed.Id, team.BaselineStartingIds);
            // Every OTHER original starter is still a starter.
            foreach (var id in starterIdsBefore.Skip(1))
                Assert.Contains(id, team.BaselineStartingIds);
            // Nobody from the original bench got silently pulled into the XI, and the bench
            // itself is untouched by a starter-side departure.
            Assert.Equal(benchIdsBefore, team.BaselineBenchIds);
        }

        [Fact]
        public void RefillBench_WhenNoBaselineWasEverConfirmed_NeverWipesTheLiveXIOrBench()
        {
            // Regression test: a squad that never went through LineupViewModel.Confirm (a
            // fresh team, an AI team, or literally any save from before the baseline feature
            // existed) has empty BaselineStartingIds/BaselineBenchIds. RefillBench used to treat
            // that exactly like RestoreBaseline's "nobody confirmed a starter" case and demote
            // the entire live XI down to Available - it must instead seed the baseline from
            // whatever is currently live before topping the bench up.
            var players = PlayerGenerator.GenerateSquad(Nationality.Germany, 60, squadSize: 25, random: new Random(4));
            int id = 1;
            foreach (var p in players)
                p.Id = id++;

            var team = new Team { Statistics = new TeamStats() };
            team.Players.AddRange(players);
            LineupSelector.SelectLineup(team, FormationCatalog.F442);
            Assert.Empty(team.BaselineStartingIds);
            Assert.Empty(team.BaselineBenchIds);

            var starterIdsBefore = team.Players.Where(p => p.Status == PlayerStatus.InStartingXI).Select(p => p.Id).OrderBy(x => x).ToList();
            var benchIdsBefore = team.Players.Where(p => p.Status == PlayerStatus.OnBench).Select(p => p.Id).ToList();
            var departed = team.Players.First(p => p.Id == benchIdsBefore[0]);
            team.Players.Remove(departed);

            LineupSelector.RefillBench(team);

            Assert.Equal(11, team.Players.Count(p => p.Status == PlayerStatus.InStartingXI));
            Assert.Equal(
                starterIdsBefore,
                team.Players.Where(p => p.Status == PlayerStatus.InStartingXI).Select(p => p.Id).OrderBy(x => x).ToList());
            Assert.Equal(9, team.Players.Count(p => p.Status == PlayerStatus.OnBench));
        }

        [Fact]
        public void RestoreBaseline_RevertsAnInMatchSubstitution()
        {
            var team = BuildFullSquadWithBaseline();
            var subOff = team.Players.First(p => p.Id == team.BaselineStartingIds[0]);
            var subOn = team.Players.First(p => p.Id == team.BaselineBenchIds[0]);

            // Simulate what a live match substitution does to Player.Status.
            subOff.Status = PlayerStatus.SubstitutedOff;
            subOn.Status = PlayerStatus.InStartingXI;

            LineupSelector.RestoreBaseline(team);

            Assert.Equal(PlayerStatus.InStartingXI, subOff.Status);
            Assert.Equal(PlayerStatus.OnBench, subOn.Status);
        }

        [Fact]
        public void RestoreBaseline_LeavesAnInjuredBaselineStarterAlone_ButReclaimsTheSlotOnceRecovered()
        {
            var team = BuildFullSquadWithBaseline();
            var injured = team.Players.First(p => p.Id == team.BaselineStartingIds[0]);
            injured.Status = PlayerStatus.Injured;

            LineupSelector.RestoreBaseline(team);
            Assert.Equal(PlayerStatus.Injured, injured.Status);

            injured.Status = PlayerStatus.Available; // recovered
            LineupSelector.RestoreBaseline(team);
            Assert.Equal(PlayerStatus.InStartingXI, injured.Status);
        }
    }
}
