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
