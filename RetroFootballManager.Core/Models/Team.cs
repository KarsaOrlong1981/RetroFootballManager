using System.Text.Json;
using RetroFootballManager.Common;
using SQLite;

namespace RetroFootballManager.Models
{
    public class Team
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        // Short form/abbreviation for compact displays (e.g. tables, tickers).
        public string ShortName { get; set; } = string.Empty;

        public Nationality Nationality { get; set; }

        // League tier 1-4 (1 = highest). Drives strength, budget, sponsors, etc.
        public int LeagueTier { get; set; }

        // File name of the crest under Resources/Images/Logos/. null/empty = placeholder.
        public string? LogoPath { get; set; }

        // Currently selected formation (name from the FormationCatalog).
        public string FormationName { get; set; } = string.Empty;

        // Playing style (counter-attack/tiki-taka/pressing/wing play/crosses to striker).
        public PlayingStyle PlayingStyle { get; set; } = PlayingStyle.CounterAttack;

        // Tactical orientation (very defensive .. very offensive) - own axis, acts as a
        // multiplier on the style factors (analogous to tackling intensity).
        public TacticalOrientation TacticalOrientation { get; set; } = TacticalOrientation.Balanced;

        // Team's default tackling intensity. Individual players can override this via
        // Player.TacklingIntensity at any time (even live during a match).
        public TacklingIntensity TacklingIntensity { get; set; } = TacklingIntensity.Normal;

        // Team-wide training focus - slowly improves (over many weeks) 1-2 matching
        // attributes across the whole squad. null = no focus set.
        public TeamTrainingFocus? TeamTrainingFocus { get; set; }

        // Which (month, year) DevelopmentService.ApplyMonthlyDevelopment last ran for this team -
        // 0/0 (never set) is never a real month/year, so no backfill is needed for old saves.
        public int LastDevelopmentMonth { get; set; }
        public int LastDevelopmentYear { get; set; }

        // Club mood (Vereinsstimmung), 0-100. See ClubMoodService - drops below 30 end the
        // career (board/fan dismissal); both below 45 trigger a warning message.
        public int FanMood { get; set; } = 65;
        public int BoardMood { get; set; } = 65;

        // Consecutive league wins for the human team - reset on a draw/loss, see ClubMoodService.
        public int CurrentWinStreak { get; set; }

        // Whether the "club mood is low" warning message has already been sent for the current
        // dip below 45% - reset once both moods recover, so it can fire again on a later dip.
        public bool ClubMoodWarningActive { get; set; }

        // Whether the "board is delighted" praise message has already been sent for the
        // current high (BoardMood > 95) - reset once it drops back below 90, so it can fire
        // again on a later high.
        public bool BoardMoodPraiseActive { get; set; }

        private List<int>? _baselineStartingCache;
        private string _baselineStartingRaw = "[]";

        // The manager's (or accepted co-trainer's) persistent starting XI, set on
        // LineupViewModel.Confirm - see LineupSelector.RestoreBaseline/RefillBench. Empty =
        // no baseline yet (old saves, AI teams), all baseline logic then no-ops. Persisted as
        // JSON, same convention as Player.SecondaryPositionsRaw.
        public string BaselineStartingRaw
        {
            get => _baselineStartingRaw;
            // sqlite-net reads an existing save's pre-migration row (column added via ALTER
            // TABLE to a table that predates it) as SQL NULL, calling this setter with a literal
            // null - never skipped, never coerced to the "[]" field initializer. Guard here so
            // an old save deserializes as "no baseline yet" instead of crashing on first access.
            set { _baselineStartingRaw = value ?? "[]"; _baselineStartingCache = null; }
        }

        [Ignore]
        public List<int> BaselineStartingIds
        {
            get => _baselineStartingCache ??= JsonSerializer.Deserialize<List<int>>(_baselineStartingRaw) ?? [];
            set { _baselineStartingCache = value; _baselineStartingRaw = JsonSerializer.Serialize(value); }
        }

        private List<int>? _baselineBenchCache;
        private string _baselineBenchRaw = "[]";

        // The manager's (or accepted co-trainer's) persistent bench - see BaselineStartingRaw.
        public string BaselineBenchRaw
        {
            get => _baselineBenchRaw;
            // See BaselineStartingRaw's setter comment - same NULL-from-migration guard.
            set { _baselineBenchRaw = value ?? "[]"; _baselineBenchCache = null; }
        }

        [Ignore]
        public List<int> BaselineBenchIds
        {
            get => _baselineBenchCache ??= JsonSerializer.Deserialize<List<int>>(_baselineBenchRaw) ?? [];
            set { _baselineBenchCache = value; _baselineBenchRaw = JsonSerializer.Serialize(value); }
        }

        [Ignore]
        public Tactic Tactic => new Tactic(PlayingStyle, TacticalOrientation);

        [Ignore]
        public List<Player> Players { get; set; } = [];

        // Youth academy prospects (persisted as Player rows with IsYouthProspect = true,
        // kept out of the senior squad so they are never auto-selected for the XI).
        [Ignore]
        public List<Player> YouthPlayers { get; set; } = [];

        [Ignore]
        public List<Employee> Employees { get; set; } = [];

        [Ignore]
        public TeamStats? Statistics { get; set; }

        [Ignore]
        public Finances? Finances { get; set; }

        [Ignore]
        public Stadium? Stadium { get; set; }

        [Ignore]
        public ClubLoan? ActiveLoan { get; set; }

        [Ignore]
        public ManagerProfile? ManagerProfile { get; set; }

        [Ignore]
        public double AverageRating => Players.Count != 0
             ? Players.Average(p => p.Rating)
             : 0;
    }
}
