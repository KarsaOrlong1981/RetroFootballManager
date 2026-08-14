using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public enum TalkType { Neutral, Praise, Criticize }
    public enum PlayerConversationOption
    {
        EncourageFutureChance,
        PraiseStrongPerformance,
        CriticizeUnderperformance,
        AddressLackOfMatchPractice,
        ConfirmKeySquadRole,
        Personal
    }

    // Applied=false means the cooldown blocked the talk - nothing changed, show ReactionText
    // as a hint instead of a reaction.
    public record TalkResult(bool Applied, int MoralDelta, int NewMoral, string ReactionText, bool WantsToLeaveClub);

    // Manager <-> player talks from the Lineup page's "Mit Spieler sprechen" dialog. Praise/
    // criticism move Player.Moral (which already feeds TrainingService.PlayerMoraleFactor),
    // and criticism can additionally grant a temporary, morale-independent TalkMotivationBoost
    // for players who are spurred on by it (see ConversationEffects).
    public static class ConversationService
    {
        private const int BasePraiseDelta = 6;
        private const int BaseCriticismDelta = -6;
        private const int BaseNeutralDelta = 4;

        // Reached after 4 talks in the same direction - the next one in that direction tips
        // over instead of applying normally (overpraise annoys him, overcriticism deepens
        // his wish to leave).
        private const int TippingStreakThreshold = 4;

        private const int UnhappyMoraleThreshold = 40;
        private const int RecoveryMoraleThreshold = 50;
        private const double MaxTalkMotivationBoost = 0.3;
        private const double WeeklyMotivationDecay = 0.8;
        private const double MotivationDecayFloor = 0.02;
        private const int UnhappyWeeklyMoraleDecay = 2;
        private const int CooldownDays = 7;

        public static TalkResult Talk(Player player, TalkType type, GameState state, int matchFactor = 2)
        {
            if (player.LastTalkDate is DateTime last && (state.CurrentDate - last).TotalDays < CooldownDays)
                return new TalkResult(false, 0, player.Moral,
                    "Ihr habt diese Woche schon gesprochen - gib ihm etwas Zeit.", player.WantsToLeaveClub);

            var sensitivity = ConversationEffects.Get(player);
            bool overpraised = type == TalkType.Praise && player.RecentTalkStreak >= TippingStreakThreshold;
            bool overcriticized = type == TalkType.Criticize && player.RecentTalkStreak <= -TippingStreakThreshold;
            bool overfamiliar = type == TalkType.Neutral && player.RecentPersonalTalkStreak >= sensitivity.PersonalTalkTolerance;

            int normalDelta = type switch
            {
                TalkType.Praise => (int)Math.Round(BasePraiseDelta * sensitivity.PraiseMoraleFactor),
                TalkType.Criticize => (int)Math.Round(BaseCriticismDelta * sensitivity.CriticismMoraleFactor),
                _ => (int)Math.Round(BaseNeutralDelta * sensitivity.NeutralMoraleFactor)
            };

            int delta;
            string reactionText;
            double motivationGain = 0;

            if (overpraised)
            {
                delta = -Math.Abs(normalDelta);
                reactionText = "Er findet das Lob langsam übertrieben und wirkt genervt - der Respekt vor dir bröckelt.";
                player.RecentTalkStreak = -1;
            }
            else if (overcriticized)
            {
                delta = normalDelta * 2;
                reactionText = "Er hat genug von der Kritik und deutet an, über einen Vereinswechsel nachzudenken.";
                player.WantsToLeaveClub = true;
                player.RecentTalkStreak -= 1;
            }
            else if (overfamiliar)
            {
                delta = -Math.Abs(normalDelta);
                reactionText = "Die vielen Gespräche nerven ihn langsam, er wünscht sich einfach etwas Ruhe von dir.";
                player.RecentPersonalTalkStreak = 0;
            }
            else if (type == TalkType.Praise)
            {
                delta = normalDelta;
                reactionText = "Er wirkt zufrieden mit dem Gespräch.";
                player.RecentTalkStreak = player.RecentTalkStreak > 0 ? player.RecentTalkStreak + 1 : 1;
            }
            else if (type == TalkType.Neutral)
            {
                delta = normalDelta;
                reactionText = "Das persönliche Gespräch tut ihm sichtlich gut, er fühlt sich wahrgenommen.";
                player.RecentPersonalTalkStreak++;
            }
            else
            {
                delta = normalDelta;
                reactionText = "Er nimmt sich die Kritik zu Herzen.";
                player.RecentTalkStreak = player.RecentTalkStreak < 0 ? player.RecentTalkStreak - 1 : -1;
                motivationGain = sensitivity.CriticismMotivationBonus;
            }

            delta = (int)Math.Round(delta * (matchFactor switch
            {
                3 => 1.2, // optimale Option +20% Wirkung
                2 => 1.0, // neutrale Option normal
                1 => 0.5, // schlechte Option halbierte Wirkung
                _ => 1.0
            }));

            string matchReaction = matchFactor switch
            {
                3 => "Das Gespräch passt perfekt zu seiner Situation.",
                2 => "Er nimmt das Gespräch neutral auf.",
                1 => "Das Gespräch passt nicht zu seiner aktuellen Lage.",
                _ => ""
            };

            reactionText = $"{matchReaction}\n{reactionText}";

            if (matchFactor == 1)
            {
                player.Moral = Math.Clamp(player.Moral - 2, 0, 100);
            }

            if (motivationGain > 0)
                player.TalkMotivationBoost = Math.Clamp(player.TalkMotivationBoost + motivationGain, 0, MaxTalkMotivationBoost);

            player.Moral = Math.Clamp(player.Moral + delta, 0, 100);

            if (player.Moral < UnhappyMoraleThreshold)
                player.WantsToLeaveClub = true;
            else if (player.Moral >= RecoveryMoraleThreshold)
                player.WantsToLeaveClub = false;

            player.LastTalkDate = state.CurrentDate;

            return new TalkResult(true, delta, player.Moral, reactionText, player.WantsToLeaveClub);
        }

        // Weekly tick (call alongside TrainingService.ApplyWeeklyTraining): decays the talk
        // motivation boost and streak, and keeps grinding down the morale of unhappy players
        // for as long as they stay unhappy - the longer he stays, the worse it gets.
        public static void ApplyWeeklyDecay(Team team)
        {
            foreach (var player in team.Players)
            {
                if (player.TalkMotivationBoost > 0)
                {
                    player.TalkMotivationBoost *= WeeklyMotivationDecay;
                    if (player.TalkMotivationBoost < MotivationDecayFloor)
                        player.TalkMotivationBoost = 0;
                }

                if (player.RecentTalkStreak > 0)
                    player.RecentTalkStreak--;
                else if (player.RecentTalkStreak < 0)
                    player.RecentTalkStreak++;

                if (player.RecentPersonalTalkStreak > 0)
                    player.RecentPersonalTalkStreak--;

                if (player.WantsToLeaveClub || player.Moral < UnhappyMoraleThreshold)
                {
                    player.Moral = Math.Clamp(player.Moral - UnhappyWeeklyMoraleDecay, 0, 100);
                    player.WantsToLeaveClub = player.Moral < RecoveryMoraleThreshold;
                }
            }
        }

        public static TalkType GetTalkType(PlayerConversationOption option)
        {
            return ConversationTypeMap.TryGetValue(option, out var type)
                ? type
                : TalkType.Praise;
        }

        public static string GetConversationText(PlayerConversationOption option)
        {
            return option switch
            {
                PlayerConversationOption.EncourageFutureChance =>
                    "Du wirst deine Chance noch bekommen, aber aktuell sind andere Spieler vor dir. Arbeite hart an dir, dann sehen wir weiter.",

                PlayerConversationOption.PraiseStrongPerformance =>
                    "Deine Leistungen waren zuletzt richtig stark, weiter so.",

                PlayerConversationOption.CriticizeUnderperformance =>
                    "Deine Leistungen waren zuletzt unter deinen Möglichkeiten, da geht mehr.",

                PlayerConversationOption.AddressLackOfMatchPractice =>
                    "Du hast bisher wenig Spielpraxis bekommen. Nutze deine Chancen, wenn sie kommen.",

                PlayerConversationOption.ConfirmKeySquadRole =>
                    "Du bist ein wichtiger Bestandteil der Mannschaft, mach so weiter.",

                _ => "Wir sollten einfach mal Sprechen und uns Kennenlernen, Also......"
            };
        }

        private static readonly Dictionary<PlayerConversationOption, TalkType> ConversationTypeMap =
            new()
            {
                { PlayerConversationOption.EncourageFutureChance, TalkType.Praise },
                { PlayerConversationOption.PraiseStrongPerformance, TalkType.Praise },
                { PlayerConversationOption.CriticizeUnderperformance, TalkType.Criticize },
                { PlayerConversationOption.AddressLackOfMatchPractice, TalkType.Criticize },
                { PlayerConversationOption.ConfirmKeySquadRole, TalkType.Praise },
                {PlayerConversationOption.Personal, TalkType.Neutral}
            };
    }
}
