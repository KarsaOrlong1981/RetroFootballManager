using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Club mood (Vereinsstimmung): two persistent 0-100 stats per team - fan mood and board
    // mood. Both dropping below WarningThreshold sends a one-time warning message; either one
    // dropping below GameOverThreshold ends the career (see CheckThresholds).
    public static class ClubMoodService
    {
        public const int WarningThreshold = 45;
        public const int GameOverThreshold = 30;
        public const int PraiseThreshold = 95;
        public const int PraiseResetThreshold = 90;

        private const int WinStreakMilestone = 5;

        public static void ApplyLeagueWin(Team team, Random? random = null)
        {
            var rng = random ?? Random.Shared;

            team.FanMood = Clamp(team.FanMood + 1);
            // Board mood reacts to individual wins too, but more mutedly than the fans.
            if (rng.NextDouble() < 0.5)
                team.BoardMood = Clamp(team.BoardMood + 1);

            team.CurrentWinStreak++;
            if (team.CurrentWinStreak > 0 && team.CurrentWinStreak % WinStreakMilestone == 0)
                team.FanMood = Clamp(team.FanMood + 3);
        }

        public static void ApplyLeagueLossOrDraw(Team team) => team.CurrentWinStreak = 0;

        public static void ApplyStadiumExpansion(Team team) => team.FanMood = Clamp(team.FanMood + 5);

        public static void ApplyCupRoundAdvance(Team team)
        {
            team.FanMood = Clamp(team.FanMood + 5);
            team.BoardMood = Clamp(team.BoardMood + 5);
        }

        public static void ApplyCupWin(Team team, CompetitionType competition)
        {
            int bonus = competition == CompetitionType.GermanCup ? 25 : 30;
            team.FanMood = Clamp(team.FanMood + bonus);
            team.BoardMood = Clamp(team.BoardMood + bonus);
        }

        // Early cup elimination only stings for top-flight clubs expected to go far - lower-tier
        // teams get no penalty for it.
        public static void ApplyCupElimination(Team team)
        {
            if (team.LeagueTier != 1)
                return;

            team.FanMood = Clamp(team.FanMood - 3);
            team.BoardMood = Clamp(team.BoardMood - 3);
        }

        public static void ApplyChampionship(Team team)
        {
            team.FanMood = Clamp(team.FanMood + 30);
            team.BoardMood = Clamp(team.BoardMood + 30);
        }

        public static void ApplyPromotion(Team team)
        {
            team.FanMood = Clamp(team.FanMood + 25);
            team.BoardMood = Clamp(team.BoardMood + 25);
        }

        public static void ApplyRelegation(Team team)
        {
            team.FanMood = Clamp(team.FanMood - 30);
            team.BoardMood = Clamp(team.BoardMood - 30);
        }

        // Checks the human team's mood thresholds - sets GameState.IsGameOver on a career-ending
        // dip, otherwise sends (or clears) the one-time low-mood warning.
        public static async Task CheckThresholds(
            Team team, GameState state, MessageService messages, DateTime currentDate)
        {
            if (team.BoardMood < GameOverThreshold)
            {
                state.IsGameOver = true;
                state.GameOverReason = "Vorstand";
                return;
            }

            if (team.FanMood < GameOverThreshold)
            {
                state.IsGameOver = true;
                state.GameOverReason = "Fans";
                return;
            }

            if (team.FanMood < WarningThreshold && team.BoardMood < WarningThreshold)
            {
                if (!team.ClubMoodWarningActive)
                {
                    team.ClubMoodWarningActive = true;
                    await messages.SendAsync(MessageType.ClubMoodWarning, "Stimmung im Verein kritisch",
                        "Fans und Vorstand sind unzufrieden - dein Job ist in Gefahr.", currentDate, team.Id);
                }
            }
            else
            {
                team.ClubMoodWarningActive = false;
            }
        }

        // A board delighted with the manager (BoardMood > 95) gets its own one-time praise
        // mail - mirrors CheckThresholds' one-shot flag pattern, reset once mood drops back
        // below PraiseResetThreshold so it can fire again on a later high.
        public static async Task CheckBoardMoodPraise(Team team, MessageService messages, DateTime currentDate)
        {
            if (team.BoardMood > PraiseThreshold)
            {
                if (!team.BoardMoodPraiseActive)
                {
                    team.BoardMoodPraiseActive = true;
                    await messages.SendAsync(MessageType.BoardPraise, "Der Vorstand ist begeistert",
                        "Der Vorstand ist überaus zufrieden mit deiner Arbeit als Trainer und Manager.",
                        currentDate, team.Id);
                }
            }
            else if (team.BoardMood < PraiseResetThreshold)
            {
                team.BoardMoodPraiseActive = false;
            }
        }

        private static int Clamp(int value) => Math.Clamp(value, 0, 100);
    }
}
