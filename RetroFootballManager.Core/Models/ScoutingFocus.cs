using System.Text.Json;
using SQLite;

namespace RetroFootballManager.Models
{
    public record AttributeFilter(PlayerAttributeType Attribute, int MinValue);

    // What a specific scout should look for, assigned via the Scouting-Fokus dialog. All
    // filters are optional - if none are set (HasAnyFilter false), ScoutingService falls back
    // to a team-weakness analysis instead of a targeted search.
    public class ScoutingFocus
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int TeamId { get; set; }

        // Which Scout Employee this focus belongs to - one active focus per scout (a new
        // assignment replaces the previous one for that same scout).
        public int ScoutEmployeeId { get; set; }

        public DateTime CreatedDate { get; set; }

        public Position? Position { get; set; }
        public int? MinAge { get; set; }
        public int? MaxAge { get; set; }
        public int? MinTalent { get; set; }
        public int? MaxTalent { get; set; }
        public int? MinRating { get; set; }
        public InMatchCharacterType? CharacterType { get; set; }
        public Personality? PersonalityType { get; set; }
        public Nationality? Nationality { get; set; }

        private List<AttributeFilter>? _attributeFiltersCache;
        private string _attributeFiltersRaw = "[]";

        // Persisted as JSON since sqlite-net can't store complex lists directly (same pattern
        // as Player.SecondaryPositionsRaw).
        public string AttributeFiltersRaw
        {
            get => _attributeFiltersRaw;
            set { _attributeFiltersRaw = value; _attributeFiltersCache = null; }
        }

        [Ignore]
        public List<AttributeFilter> AttributeFilters
        {
            get => _attributeFiltersCache ??= JsonSerializer.Deserialize<List<AttributeFilter>>(AttributeFiltersRaw) ?? [];
            set { AttributeFiltersRaw = JsonSerializer.Serialize(value); _attributeFiltersCache = value; }
        }

        [Ignore]
        public bool HasAnyFilter =>
            Position is not null || MinAge is not null || MaxAge is not null || MinTalent is not null
            || MaxTalent is not null || MinRating is not null || CharacterType is not null || PersonalityType is not null
            || Nationality is not null || AttributeFilters.Count > 0;
    }
}
