using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public record TrainingCampOption(TrainingCampTier Tier, int DurationWeeks, double Cost, int MoraleBoost, bool GrantsAttributeBoost);

    // Cost/effect table per tier x duration - the pricier and longer, the more effective. Only
    // the most expensive+longest combination (Elite, 2 weeks) also grants an attribute boost.
    public static class TrainingCampCatalog
    {
        public static readonly IReadOnlyList<TrainingCampOption> Options =
        [
            new(TrainingCampTier.Basic, 1, 20_000, 8, false),
            new(TrainingCampTier.Basic, 2, 35_000, 12, false),
            new(TrainingCampTier.Advanced, 1, 50_000, 14, false),
            new(TrainingCampTier.Advanced, 2, 90_000, 20, false),
            new(TrainingCampTier.Elite, 1, 100_000, 22, false),
            new(TrainingCampTier.Elite, 2, 180_000, 30, true),
        ];

        public static TrainingCampOption Get(TrainingCampTier tier, int durationWeeks) =>
            Options.First(o => o.Tier == tier && o.DurationWeeks == durationWeeks);
    }
}
