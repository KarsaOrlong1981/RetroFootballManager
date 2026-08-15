using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class InMatchCharacterTests
    {
        [Theory]
        [InlineData(TeamTalkOption.StayCalm, 0, 0, 3)]
        [InlineData(TeamTalkOption.CheerOn, 0, 2, 8)]
        [InlineData(TeamTalkOption.Praise, 2, 0, 7)]
        [InlineData(TeamTalkOption.Criticize, 3, 0, -6)]
        [InlineData(TeamTalkOption.Shout, 0, 2, 4)]
        [InlineData(TeamTalkOption.TacticalTalk, 1, 1, 1)]
        [InlineData(TeamTalkOption.ExpressConfidence, 1, 1, 5)]
        [InlineData(TeamTalkOption.WarnAgainstComplacency, 1, 1, 0)]
        [InlineData(TeamTalkOption.EmotionalBuildUp, 1, 1, 12)]
        [InlineData(TeamTalkOption.SayNothing, 1, 1, 0)]
        public void TryApply_MatchesExpectedDeltaForNeutralCharacter(
            TeamTalkOption option, int homeGoals, int awayGoals, int expectedDelta)
        {
            var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
            var away = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
            var match = new Match(home, away);
            match.Result.HomeGoals = homeGoals;
            match.Result.AwayGoals = awayGoals;
            foreach (var p in home.Players) p.InMatchMoral = 50;

            TeamTalkService.TryApply(match, isHome: true, option);

            Assert.All(home.Players, p => Assert.Equal(50 + expectedDelta, p.InMatchMoral));
        }

        [Fact]
        public void TryApply_EmotionalBuildUp_OnlyUsableOncePerMatchPerSide()
        {
            var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
            var away = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
            var match = new Match(home, away);

            bool firstHome = TeamTalkService.TryApply(match, isHome: true, TeamTalkOption.EmotionalBuildUp);
            bool secondHome = TeamTalkService.TryApply(match, isHome: true, TeamTalkOption.EmotionalBuildUp);
            bool firstAway = TeamTalkService.TryApply(match, isHome: false, TeamTalkOption.EmotionalBuildUp);

            Assert.True(firstHome);
            Assert.False(secondHome);
            Assert.True(firstAway);
        }

        [Fact]
        public void TryApply_Shout_BackfiresForHothead_ButNotForNeutralCharacter_WhenLeading()
        {
            var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
            var away = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
            var hothead = home.Players[0];
            hothead.InMatchCharacter = InMatchCharacterType.Hothead;
            var neutral = home.Players[1];
            foreach (var p in home.Players) p.InMatchMoral = 50;

            var match = new Match(home, away);
            match.Result.HomeGoals = 2; // comfortably leading

            TeamTalkService.TryApply(match, isHome: true, TeamTalkOption.Shout);

            Assert.True(hothead.InMatchMoral > 50, $"hothead={hothead.InMatchMoral}");
            Assert.True(neutral.InMatchMoral < 50, $"neutral={neutral.InMatchMoral}");
        }

        [Fact]
        public void Simulate_OverManyMismatchedMatches_FighterEndsWithHigherAverageMoralThanIceCold_WhenUsuallyBehind()
        {
            // A big rating gap (not forced scoreline overrides, which don't stop real
            // RegisterGoal calls from firing their own ApplyGoalMoraleReactions and
            // contaminating the result) - the weak home side is behind in most matches,
            // giving BehindResilience (Fighter) a real, repeated chance to matter versus
            // the neutral 1.0 default (IceCold's BehindResilience is unset).
            var random = new Random(20);
            double fighterMoralSum = 0, iceColdMoralSum = 0;
            const int matches = 40;

            for (int i = 0; i < matches; i++)
            {
                var home = TestHelpers.CreateTeam("Schwach", baseRating: 25);
                var fighter = home.Players[0];
                fighter.InMatchCharacter = InMatchCharacterType.Fighter;
                var iceCold = home.Players[1];
                iceCold.InMatchCharacter = InMatchCharacterType.IceCold;

                var away = TestHelpers.CreateTeam("Stark", baseRating: 90);
                var match = new Match(home, away, random);
                match.Simulate();

                fighterMoralSum += fighter.InMatchMoral;
                iceColdMoralSum += iceCold.InMatchMoral;
            }

            double avgFighter = fighterMoralSum / matches;
            double avgIceCold = iceColdMoralSum / matches;
            Assert.True(avgFighter > avgIceCold, $"fighter={avgFighter}, iceCold={avgIceCold}");
        }

        [Fact]
        public void AiMatchCoach_ChoosesCheerOn_WhenBehindAtSecondHalfStart()
        {
            var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
            var away = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
            var match = new Match(home, away, new Random(11));
            match.Begin();
            while (match.Phase != MatchPhase.SecondHalf)
                match.AdvanceMinute();

            match.Result.HomeGoals = 0;
            match.Result.AwayGoals = 2;
            foreach (var p in match.OnPitch(isHome: true)) p.InMatchMoral = 50;

            new AiMatchCoach().OnMinute(match, isHome: true, match.CurrentMinute);

            Assert.True(match.OnPitch(isHome: true).All(p => p.InMatchMoral > 50));
        }

        [Fact]
        public void AiMatchCoach_ChoosesWarnAgainstComplacency_WhenLeadingBigAtSecondHalfStart()
        {
            var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
            var away = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
            var complacentPlayer = home.Players[0];
            complacentPlayer.InMatchCharacter = InMatchCharacterType.Complacent;
            var neutralPlayer = home.Players[1];

            var match = new Match(home, away, new Random(12));
            match.Begin();
            while (match.Phase != MatchPhase.SecondHalf)
                match.AdvanceMinute();

            match.Result.HomeGoals = 3;
            match.Result.AwayGoals = 0;
            foreach (var p in match.OnPitch(isHome: true)) p.InMatchMoral = 50;

            new AiMatchCoach().OnMinute(match, isHome: true, match.CurrentMinute);

            // Complacent benefits from the extra warning (protects against his own trait);
            // a neutral player has nothing to warn against, so the talk is a no-op for him.
            Assert.True(complacentPlayer.InMatchMoral > 50, $"complacent={complacentPlayer.InMatchMoral}");
            Assert.Equal(50, neutralPlayer.InMatchMoral);
        }

        [Fact]
        public void AiMatchCoach_GivesTeamTalkExactlyOnce_AcrossMultipleSecondHalfCalls()
        {
            var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
            var away = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
            var match = new Match(home, away, new Random(14));
            match.Begin();
            while (match.Phase != MatchPhase.SecondHalf)
                match.AdvanceMinute();

            match.Result.HomeGoals = 0;
            match.Result.AwayGoals = 2;
            foreach (var p in match.OnPitch(isHome: true)) p.InMatchMoral = 50;

            var coach = new AiMatchCoach();
            coach.OnMinute(match, isHome: true, match.CurrentMinute);
            int moralAfterFirstCall = match.OnPitch(isHome: true).First().InMatchMoral;

            coach.OnMinute(match, isHome: true, match.CurrentMinute + 1);
            int moralAfterSecondCall = match.OnPitch(isHome: true).First().InMatchMoral;

            Assert.Equal(moralAfterFirstCall, moralAfterSecondCall);
        }

        [Fact]
        public void Simulate_OverManyMatches_TeamThatScoresMoreEndsWithHigherAverageInMatchMoral()
        {
            var random = new Random(80);
            double scoredMoreMoralSum = 0, concededMoreMoralSum = 0;
            int scoredMoreCount = 0, concededMoreCount = 0;

            for (int i = 0; i < 60; i++)
            {
                var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
                var away = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
                var match = new Match(home, away, random);
                var result = match.Simulate();

                double homeAvgMoral = match.OnPitch(isHome: true).Average(p => p.InMatchMoral);
                double awayAvgMoral = match.OnPitch(isHome: false).Average(p => p.InMatchMoral);

                if (result.HomeGoals > result.AwayGoals)
                {
                    scoredMoreMoralSum += homeAvgMoral;
                    concededMoreMoralSum += awayAvgMoral;
                    scoredMoreCount++;
                    concededMoreCount++;
                }
                else if (result.AwayGoals > result.HomeGoals)
                {
                    scoredMoreMoralSum += awayAvgMoral;
                    concededMoreMoralSum += homeAvgMoral;
                    scoredMoreCount++;
                    concededMoreCount++;
                }
            }

            Assert.True(scoredMoreCount > 0 && concededMoreCount > 0);
            double avgScoredMore = scoredMoreMoralSum / scoredMoreCount;
            double avgConcededMore = concededMoreMoralSum / concededMoreCount;
            Assert.True(avgScoredMore > avgConcededMore, $"scoredMore={avgScoredMore}, concededMore={avgConcededMore}");
        }

        [Fact]
        public void SeedInMatchMoral_SlowStarter_GetsOneTimeDeductionAtKickoff()
        {
            var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
            var slowStarter = home.Players[0];
            slowStarter.InMatchCharacter = InMatchCharacterType.SlowStarter;
            var neutral = home.Players[1];
            var away = TestHelpers.CreateTeam("Gast FC", baseRating: 65);

            var match = new Match(home, away, new Random(15));
            match.Begin();

            Assert.True(slowStarter.InMatchMoral < neutral.InMatchMoral,
                $"slowStarter={slowStarter.InMatchMoral}, neutral={neutral.InMatchMoral}");
        }
    }
}
