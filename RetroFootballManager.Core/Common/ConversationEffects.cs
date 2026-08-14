using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // How strongly a player's morale reacts to being praised/criticized/talked to personally,
    // and whether criticism additionally motivates him (ConversationService.TalkMotivationBoost).
    // PraiseMoraleFactor/CriticismMoraleFactor/NeutralMoraleFactor scale the base delta (1.0 =
    // normal, higher = stronger swing); CriticismMotivationBonus is added straight to
    // TalkMotivationBoost. PersonalTalkTolerance is how many personal talks in a row (see
    // Player.RecentPersonalTalkStreak) a player accepts before the next one feels intrusive
    // instead of appreciated (lower = shorter fuse for small talk).
    public readonly record struct ConversationSensitivity(
        double PraiseMoraleFactor = 1.0,
        double CriticismMoraleFactor = 1.0,
        double CriticismMotivationBonus = 0.0,
        double NeutralMoraleFactor = 1.0,
        int PersonalTalkTolerance = 3);

    public static class ConversationEffects
    {
        public static ConversationSensitivity Get(Player player) => player.Personality switch
        {
            // Self-assured, doesn't need small talk to feel valued, but tolerates it well.
            Personality.Leader => new ConversationSensitivity(
                PraiseMoraleFactor: 0.7, CriticismMoraleFactor: 0.7,
                NeutralMoraleFactor: 0.6, PersonalTalkTolerance: 5),

            // Short-tempered - personal attention calms them more, but feeling "handled" too
            // often annoys them fast.
            Personality.Hothead or Personality.Enforcer => new ConversationSensitivity(
                PraiseMoraleFactor: 1.0, CriticismMoraleFactor: 1.6,
                NeutralMoraleFactor: 1.2, PersonalTalkTolerance: 2),

            Personality.Maestro or Personality.Technician => new ConversationSensitivity(
                PraiseMoraleFactor: 1.3, CriticismMoraleFactor: 1.3,
                NeutralMoraleFactor: 1.1, PersonalTalkTolerance: 3),

            // Loyal, appreciates being noticed personally and doesn't mind frequent chats.
            Personality.Workhorse => new ConversationSensitivity(
                PraiseMoraleFactor: 0.9, CriticismMoraleFactor: 0.8, CriticismMotivationBonus: 0.12,
                NeutralMoraleFactor: 1.2, PersonalTalkTolerance: 4),

            Personality.Strategist or Personality.Sprinter or Personality.HeaderBeast =>
                new ConversationSensitivity(),

            // None (and any unmapped value): fall back to age/talent - young, talented
            // players shrug off criticism and it spurs them on; old, low-talent players
            // barely react either way.
            _ => AgeTalentFallback(player.Age, player.Talent),
        };

        private static ConversationSensitivity AgeTalentFallback(int age, int talent)
        {
            // 0 = as young/talented as it gets (≤19, talent 90), 1 = as old/limited as it
            // gets (≥33, talent 30) - interpolate both axes and average them.
            double ageT = Math.Clamp((age - 19) / 14.0, 0.0, 1.0);
            double talentT = Math.Clamp((90 - talent) / 60.0, 0.0, 1.0);
            double veteranT = (ageT + talentT) / 2.0;

            double criticismFactor = double.Lerp(0.6, 1.0, veteranT);
            double praiseFactor = double.Lerp(0.9, 1.1, veteranT);
            double motivationBonus = double.Lerp(0.15, 0.0, veteranT);
            // Young players value the manager's personal attention more and tolerate it
            // longer; veterans have seen it all and get annoyed with it sooner.
            double neutralFactor = double.Lerp(1.2, 0.8, veteranT);
            int personalTolerance = (int)Math.Round(double.Lerp(4, 2, veteranT));

            return new ConversationSensitivity(praiseFactor, criticismFactor, motivationBonus,
                neutralFactor, personalTolerance);
        }
    }
}
