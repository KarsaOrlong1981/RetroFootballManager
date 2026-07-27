using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class LiveMatchAndAiTests
    {
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
        public void Engine_PausesAtHalfTime_ThenResumes()
        {
            var home = BuildMatchReadyTeam("Heim", 11);
            var away = BuildMatchReadyTeam("Gast", 12);
            var match = new Match(home, away, new Random(3));

            match.Begin();
            while (match.Phase == MatchPhase.FirstHalf)
                match.AdvanceMinute();

            Assert.Equal(MatchPhase.HalfTime, match.Phase);
            Assert.True(match.CurrentMinute >= 45);
            Assert.Contains(match.Result.Events, e => e.Type == GameEventType.HalfTime);

            int minuteAtHalf = match.CurrentMinute;
            match.AdvanceMinute(); // resume
            Assert.Equal(MatchPhase.SecondHalf, match.Phase);
            Assert.True(match.CurrentMinute > minuteAtHalf);

            while (!match.IsFinished)
                match.AdvanceMinute();
            Assert.Contains(match.Result.Events, e => e.Type == GameEventType.FullTime);
        }

        [Fact]
        public void StepwiseSimulate_MatchesInternalResult()
        {
            var home = BuildMatchReadyTeam("Heim", 13);
            var away = BuildMatchReadyTeam("Gast", 14);

            var match = new Match(home, away, new Random(9));
            var result = match.Simulate();

            Assert.True(match.IsFinished);
            Assert.Equal(result.HomeGoals, result.HomeScorers.Count);
        }

        [Fact]
        public void AiCoach_SwitchesToOffensiveWhenLosing()
        {
            bool sawOffensiveSwitch = false;

            for (int seed = 0; seed < 12 && !sawOffensiveSwitch; seed++)
            {
                var strongHome = BuildMatchReadyTeam("Stark", 100 + seed, rating: 90);
                var weakAway = BuildMatchReadyTeam("Schwach", 200 + seed, rating: 25);

                var match = new Match(strongHome, weakAway, new Random(seed + 1))
                {
                    // Only the trailing away side is AI-controlled here.
                    AwayCoach = new AiMatchCoach(),
                };
                var result = match.Simulate();

                if (result.Events.Any(e => e.Type == GameEventType.TacticChange
                                           && !e.IsHomeTeam
                                           && e.Description.Contains("Offensiv")))
                    sawOffensiveSwitch = true;
            }

            Assert.True(sawOffensiveSwitch,
                "Der KI-Trainer sollte bei deutlichem Rückstand auf Offensiv umstellen.");
        }

        [Fact]
        public void AiCoach_MakesSubstitutionsOverAFullMatch()
        {
            var home = BuildMatchReadyTeam("Heim", 21);
            var away = BuildMatchReadyTeam("Gast", 22);

            var match = new Match(home, away, new Random(4))
            {
                HomeCoach = new AiMatchCoach(),
                AwayCoach = new AiMatchCoach(),
            };
            match.Simulate();

            Assert.True(match.SubsUsed(true) > 0, "Die KI sollte müde Spieler auswechseln.");
            Assert.True(match.SubsUsed(true) <= Match.MaxSubstitutions);
        }
    }
}
