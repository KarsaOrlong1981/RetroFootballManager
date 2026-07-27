using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public record SeasonPhaseInfo(SeasonPhase Phase, TransferWindowState TransferWindow);

    // Derives the current season phase and transfer-window state purely from the season's
    // fixture dates/matchday index - no extra persisted state needed. The transfer window is
    // open pre-season, for the first 4 matchdays of each half, and throughout the winter break
    // (see FixtureGenerator.WinterBreakWeeks for the calendar gap this relies on); closed the
    // rest of the season. Modeling the window state only - actual transfer mechanics are a
    // later milestone.
    public static class SeasonPhaseCalculator
    {
        public const int TransferWindowMatchdays = 4;

        public static SeasonPhaseInfo Calculate(DateTime currentDate, int matchdayIndex, IReadOnlyList<Fixture> seasonFixtures)
        {
            if (seasonFixtures.Count == 0)
                return new SeasonPhaseInfo(SeasonPhase.PreSeason, TransferWindowState.Open);

            var firstFixtureDate = seasonFixtures.Min(f => f.Date).Date;
            if (currentDate.Date < firstFixtureDate)
                return new SeasonPhaseInfo(SeasonPhase.PreSeason, TransferWindowState.Open);

            int totalMatchdays = seasonFixtures.Max(f => f.Matchday);
            int firstHalfCount = totalMatchdays / 2;

            var firstHalfLastDate = seasonFixtures.Where(f => f.Matchday == firstHalfCount).Max(f => f.Date).Date;
            var secondHalfFirstDate = seasonFixtures.Where(f => f.Matchday == firstHalfCount + 1).Min(f => f.Date).Date;

            if (currentDate.Date > firstHalfLastDate && currentDate.Date < secondHalfFirstDate)
                return new SeasonPhaseInfo(SeasonPhase.WinterBreak, TransferWindowState.Open);

            var phase = currentDate.Date <= firstHalfLastDate ? SeasonPhase.FirstHalf : SeasonPhase.SecondHalf;
            int matchdayInHalf = phase == SeasonPhase.FirstHalf ? matchdayIndex : matchdayIndex - firstHalfCount;
            var window = matchdayInHalf <= TransferWindowMatchdays ? TransferWindowState.Open : TransferWindowState.Closed;

            return new SeasonPhaseInfo(phase, window);
        }
    }
}
