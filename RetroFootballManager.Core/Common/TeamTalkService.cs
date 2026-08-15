using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Applies a half-time team-talk option to every fielded player of one side. Single
    // source of logic for both the human UI (MatchDayViewModel.SelectTeamTalk) and the AI
    // (AiMatchCoach) - neither has its own copy of the deltas.
    public static class TeamTalkService
    {
        private enum Scoreline { Behind, Level, Leading, LeadingBig }

        private const int BigLeadGoalDiff = 2;

        // Returns false without applying anything if the option can't be used right now
        // (currently only "Emotional aufbauen", once per match per side).
        public static bool TryApply(Match match, bool isHome, TeamTalkOption option)
        {
            if (option == TeamTalkOption.EmotionalBuildUp && match.HasUsedEmotionalTeamTalk(isHome))
                return false;

            int goalDiff = isHome ? match.HomeGoals - match.AwayGoals : match.AwayGoals - match.HomeGoals;
            var scoreline = Classify(goalDiff);
            var manager = isHome ? match.HomeTeam.ManagerProfile : match.AwayTeam.ManagerProfile;
            double motivationFactor = ManagerEffects.MotivationFactor(manager);

            foreach (var player in match.OnPitch(isHome))
            {
                int delta = (int)Math.Round(CalculateDelta(option, scoreline, player) * motivationFactor);
                player.InMatchMoral = Math.Clamp(player.InMatchMoral + delta, 0, 100);
            }

            if (option == TeamTalkOption.EmotionalBuildUp)
                match.MarkEmotionalTeamTalkUsed(isHome);

            if (option != TeamTalkOption.SayNothing)
                ManagerGrowthService.ApplyTalkGrowth(manager);

            return true;
        }

        private static Scoreline Classify(int goalDiff) => goalDiff switch
        {
            <= -1 => Scoreline.Behind,
            0 => Scoreline.Level,
            1 => Scoreline.Leading,
            _ => Scoreline.LeadingBig,
        };

        private static int CalculateDelta(TeamTalkOption option, Scoreline scoreline, Player player)
        {
            var mod = InMatchCharacterEffects.Get(player.InMatchCharacter);

            int baseDelta = option switch
            {
                TeamTalkOption.StayCalm => scoreline == Scoreline.Behind ? 5 : 3,

                TeamTalkOption.CheerOn => scoreline switch
                {
                    Scoreline.Behind => 8,
                    Scoreline.Level => 5,
                    Scoreline.Leading => 3,
                    _ => 2,
                },

                TeamTalkOption.Praise => scoreline switch
                {
                    Scoreline.Behind => 3,
                    Scoreline.Level => 5,
                    _ => 7,
                },

                // Constructive when behind (a wake-up call), demoralizing the better things
                // are going - scaled by CriticismSensitivity (FragileConfidence takes it hardest).
                TeamTalkOption.Criticize => (int)Math.Round(scoreline switch
                {
                    Scoreline.Behind => 2.0,
                    Scoreline.Level => -2.0 * mod.CriticismSensitivity,
                    Scoreline.Leading => -4.0 * mod.CriticismSensitivity,
                    _ => -6.0 * mod.CriticismSensitivity,
                }),

                // Fires the team up when behind, backfires the better things are going -
                // Hothead reacts to being yelled at with the OPPOSITE sign of everyone else.
                TeamTalkOption.Shout => ShoutDelta(scoreline, player),

                TeamTalkOption.TacticalTalk => 1,

                // Reassurance helps everyone a little, fragile/criticism-sensitive characters
                // (FragileConfidence, NervousUnderPressure) benefit noticeably more.
                TeamTalkOption.ExpressConfidence => (int)Math.Round(5 + ((mod.CriticismSensitivity - 1.0) * 4)),

                // Only meaningful when actually ahead - directly counters LeadComplacency
                // (Complacent/LazyWhenLeading), a no-op otherwise.
                TeamTalkOption.WarnAgainstComplacency =>
                    scoreline is Scoreline.Leading or Scoreline.LeadingBig
                        ? (int)Math.Round((mod.LeadComplacency - 1.0) * 5)
                        : 0,

                TeamTalkOption.EmotionalBuildUp => 12,

                TeamTalkOption.SayNothing => 0,

                _ => 0,
            };

            return baseDelta;
        }

        private static int ShoutDelta(Scoreline scoreline, Player player)
        {
            double delta = scoreline switch
            {
                Scoreline.Behind => 4,
                Scoreline.Level => -2,
                Scoreline.Leading => -6,
                _ => -8,
            };

            if (player.InMatchCharacter == InMatchCharacterType.Hothead)
                delta = -delta;

            return (int)Math.Round(delta);
        }
    }
}
