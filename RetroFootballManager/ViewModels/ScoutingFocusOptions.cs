using RetroFootballManager.Core.Models;
using RetroFootballManager.Models;

namespace RetroFootballManager.ViewModels
{
    // Picker options for the Scouting-Fokus dialog - each list leads with a "Beliebig" (any)
    // null entry, since every filter is optional.
    public record PositionFilterOption(Position? Value, string Label)
    {
        public static readonly IReadOnlyList<PositionFilterOption> All =
            new List<PositionFilterOption> { new(null, "Beliebig") }
                .Concat(Enum.GetValues<Position>().Select(p => new PositionFilterOption(p, PositionDisplay.Short(p))))
                .ToList();
    }

    public record CharacterFilterOption(InMatchCharacterType? Value, string Label)
    {
        public static readonly IReadOnlyList<CharacterFilterOption> All =
            new List<CharacterFilterOption> { new(null, "Beliebig") }
                .Concat(Enum.GetValues<InMatchCharacterType>().Select(c => new CharacterFilterOption(c, InMatchCharacterDisplay.Name(c))))
                .ToList();
    }

    public record PersonalityFilterOption(Personality? Value, string Label)
    {
        public static readonly IReadOnlyList<PersonalityFilterOption> All =
            new List<PersonalityFilterOption> { new(null, "Beliebig") }
                .Concat(Enum.GetValues<Personality>().Select(p => new PersonalityFilterOption(p, PersonalityDisplay.Name(p))))
                .ToList();
    }

    public record NationalityFilterOption(Nationality? Value, string Label)
    {
        public static readonly IReadOnlyList<NationalityFilterOption> All =
            new List<NationalityFilterOption> { new(null, "Beliebig") }
                .Concat(Enum.GetValues<Nationality>().Select(n => new NationalityFilterOption(n, n.ToString())))
                .ToList();
    }

    public record AttributeFilterOption(PlayerAttributeType? Value, string Label)
    {
        public static readonly IReadOnlyList<AttributeFilterOption> All =
            new List<AttributeFilterOption> { new(null, "Keins") }
                .Concat(Enum.GetValues<PlayerAttributeType>().Select(a => new AttributeFilterOption(a, a.ToString())))
                .ToList();
    }
}
