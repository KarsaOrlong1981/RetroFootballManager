using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class ConversationServiceTests
    {
        private static readonly DateTime Day0 = new(2027, 8, 1);

        private static Player MakePlayer(int age = 25, int talent = 55, int moral = 50,
            Personality personality = Personality.None, int id = 1) => new()
        {
            Id = id,
            Age = age,
            Talent = talent,
            DateOfBirth = Day0.AddYears(-age),
            Personality = personality,
            Moral = moral,
            Status = PlayerStatus.Available,
        };

        private static Team MakeTeam(params Player[] players)
        {
            var team = new Team { Statistics = new TeamStats() };
            foreach (var p in players)
                team.Players.Add(p);
            return team;
        }

        private static GameState StateOn(DateTime date) => new() { CurrentDate = date };

        [Fact]
        public void Praise_RaisesMoral_Criticize_LowersMoral()
        {
            var praised = MakePlayer(moral: 50);
            var criticized = MakePlayer(moral: 50, id: 2);

            var praiseResult = ConversationService.Talk(praised, TalkType.Praise, StateOn(Day0));
            var criticizeResult = ConversationService.Talk(criticized, TalkType.Criticize, StateOn(Day0));

            Assert.True(praiseResult.Applied);
            Assert.True(praised.Moral > 50);
            Assert.True(criticizeResult.Applied);
            Assert.True(criticized.Moral < 50);
        }

        [Fact]
        public void Criticize_HothandLosesMoreMoralThanLeader()
        {
            var hothead = MakePlayer(moral: 50, personality: Personality.Hothead);
            var leader = MakePlayer(moral: 50, personality: Personality.Leader, id: 2);

            ConversationService.Talk(hothead, TalkType.Criticize, StateOn(Day0));
            ConversationService.Talk(leader, TalkType.Criticize, StateOn(Day0));

            int hotheadLoss = 50 - hothead.Moral;
            int leaderLoss = 50 - leader.Moral;
            Assert.True(hotheadLoss > leaderLoss, $"hothead lost {hotheadLoss}, leader lost {leaderLoss}");
        }

        [Fact]
        public void Criticize_YoungTalentedNoneFallback_GainsMotivationBoost_OldLowTalentDoesNot()
        {
            var youngTalent = MakePlayer(age: 19, talent: 85, moral: 50, personality: Personality.None);
            var oldVeteran = MakePlayer(age: 33, talent: 35, moral: 50, personality: Personality.None, id: 2);

            ConversationService.Talk(youngTalent, TalkType.Criticize, StateOn(Day0));
            ConversationService.Talk(oldVeteran, TalkType.Criticize, StateOn(Day0));

            Assert.True(youngTalent.TalkMotivationBoost > 0.1, $"young talent boost was {youngTalent.TalkMotivationBoost}");
            Assert.True(oldVeteran.TalkMotivationBoost < youngTalent.TalkMotivationBoost);
        }

        [Fact]
        public void Overpraise_TipsIntoNegativeReaction_AfterRepeatedPraise()
        {
            var player = MakePlayer(moral: 50, personality: Personality.Leader);
            var date = Day0;
            TalkResult? last = null;

            for (int i = 0; i < 5; i++)
            {
                last = ConversationService.Talk(player, TalkType.Praise, StateOn(date));
                date = date.AddDays(7);
            }

            Assert.NotNull(last);
            Assert.True(last!.MoralDelta < 0, $"5th praise delta was {last.MoralDelta}");
        }

        [Fact]
        public void Overcriticism_SetsWantsToLeaveClub_AfterRepeatedCriticism()
        {
            var player = MakePlayer(moral: 70, personality: Personality.Leader);
            var date = Day0;

            for (int i = 0; i < 5; i++)
            {
                ConversationService.Talk(player, TalkType.Criticize, StateOn(date));
                date = date.AddDays(7);
            }

            Assert.True(player.WantsToLeaveClub);
        }

        [Fact]
        public void MoralBelow40_SetsWantsToLeaveClub_RegardlessOfStreak()
        {
            var player = MakePlayer(moral: 41, personality: Personality.None);

            ConversationService.Talk(player, TalkType.Criticize, StateOn(Day0));

            Assert.True(player.Moral < 40);
            Assert.True(player.WantsToLeaveClub);
        }

        [Fact]
        public void WeeklyDecay_KeepsLoweringMoral_WhileUnhappy()
        {
            var player = MakePlayer(moral: 35);
            player.WantsToLeaveClub = true;
            var team = MakeTeam(player);

            ConversationService.ApplyWeeklyDecay(team);
            Assert.Equal(33, player.Moral);
            Assert.True(player.WantsToLeaveClub);

            ConversationService.ApplyWeeklyDecay(team);
            Assert.Equal(31, player.Moral);
            Assert.True(player.WantsToLeaveClub);
        }

        [Fact]
        public void Praise_AboveRecoveryThreshold_ClearsWantsToLeaveClub_AndStopsFurtherDecay()
        {
            // Recovery happens via Talk() reaching >=50, not via ApplyWeeklyDecay (which always
            // subtracts before checking the threshold, so it can only spiral down, never up).
            var player = MakePlayer(moral: 48, personality: Personality.Leader);
            player.WantsToLeaveClub = true;
            var team = MakeTeam(player);

            var result = ConversationService.Talk(player, TalkType.Praise, StateOn(Day0));

            Assert.True(player.Moral >= 50, $"moral was {player.Moral}");
            Assert.False(result.WantsToLeaveClub);
            Assert.False(player.WantsToLeaveClub);

            int moralAfterPraise = player.Moral;
            ConversationService.ApplyWeeklyDecay(team);
            Assert.Equal(moralAfterPraise, player.Moral);
        }

        [Fact]
        public void WeeklyDecay_DecaysMotivationBoostAndStreak()
        {
            var player = MakePlayer();
            player.TalkMotivationBoost = 0.25;
            player.RecentTalkStreak = 3;
            var team = MakeTeam(player);

            ConversationService.ApplyWeeklyDecay(team);

            Assert.Equal(0.2, player.TalkMotivationBoost, 3);
            Assert.Equal(2, player.RecentTalkStreak);
        }

        [Fact]
        public void SecondTalk_WithinSameWeek_IsBlockedByCooldown()
        {
            var player = MakePlayer(moral: 50);

            ConversationService.Talk(player, TalkType.Praise, StateOn(Day0));
            int moralAfterFirst = player.Moral;
            var second = ConversationService.Talk(player, TalkType.Praise, StateOn(Day0.AddDays(2)));

            Assert.False(second.Applied);
            Assert.Equal(moralAfterFirst, player.Moral);
        }
    }
}
