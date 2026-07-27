using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // AI counterpart to MainMenuViewModel's training camp dialog: COM teams periodically book a
    // camp they can afford during pre-season/winter break - frequency scales with Difficulty
    // (Easy = rare, Hard = more often), selection is purely budget-driven.
    public static class TrainingCampAiService
    {
        private static double ActivityChance(Difficulty difficulty) => difficulty switch
        {
            Difficulty.Easy => 0.05,
            Difficulty.Hard => 0.20,
            _ => 0.10,
        };

        // Returns true if an actual booking happened - CalendarAdvanceService uses this to only
        // save teams that really changed instead of the whole league every day.
        public static async Task<bool> TryBookCampAsync(
            Team team, DateTime currentDate, DateTime? windowEnd, Difficulty difficulty,
            TrainingCampService trainingCamps, Random rng)
        {
            if (windowEnd is null || team.Finances is null)
                return false;

            if (rng.NextDouble() > ActivityChance(difficulty))
                return false;

            // Like ClubManagementAiService.TryUpgradeStadium: balance must still cover the cost
            // itself afterward so the team doesn't go into the red. Among the affordable options
            // that fit the time window, the most expensive (= most effective) is chosen - the AI
            // doesn't deliberately buy the cheapest camp.
            var affordable = TrainingCampCatalog.Options
                .Where(o => currentDate.AddDays(o.DurationWeeks * 7) <= windowEnd.Value)
                .Where(o => team.Finances.CurrentBalance > o.Cost * 2)
                .OrderByDescending(o => o.Cost)
                .FirstOrDefault();
            if (affordable is null)
                return false;

            var (allowed, _) = await trainingCamps.CanBookAsync(team.Id, affordable.DurationWeeks, currentDate, windowEnd);
            if (!allowed)
                return false;

            await trainingCamps.BookAsync(team, affordable.Tier, affordable.DurationWeeks, currentDate);
            return true;
        }
    }
}
