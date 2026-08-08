using System.Text.Json;
using RetroFootballManager.Core.Models;
using SQLite;

namespace RetroFootballManager.Models
{
    public class Player
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int TeamId { get; set; }

        public string Name { get; set; } = string.Empty;

        // Age in years. Kept in storage (display/compatibility) and refreshed by
        // DevelopmentService from DateOfBirth + current game date.
        public int Age { get; set; }

        // Date of birth - basis for aging over the years.
        public DateTime DateOfBirth { get; set; }

        // Potential 1-99: the higher, the faster the player develops through
        // training and appearances (see TrainingService/DevelopmentService).
        public int Talent { get; set; }

        // Career counter (across seasons) - first-team minutes played, gives mainly young
        // players a development boost.
        public int CareerMinutesPlayed { get; set; }
        public int CareerAppearances { get; set; }

        // Minutes played this season (used for the development boost at season change,
        // then reset).
        public int SeasonMinutes { get; set; }

        // Currently trained attribute - a deliberate choice (not repeatedly clickable) that
        // slowly brings progress over many weeks (see TrainingService).
        // null = no individual training focus set.
        public TrainableAttribute? CurrentTrainingFocus { get; set; }

        // Youth player (15-19). Can mature faster via a mentor (MentorId).
        public bool IsYouthProspect { get; set; }
        public int? MentorId { get; set; }

        // Whether the manager knows this player's full abilities. At career start true for
        // the user's own squad (incl. youth), false for all other clubs - until a scout
        // observes them later (not yet built).
        public bool IsScouted { get; set; }
        public bool UsedAsWingBack {  get; set; }
        public Nationality Nationality { get; set; }

        // Natural position - the player plays at full strength here.
        public Position Position { get; set; }

        // Where the player is currently lined up/used. null = natural position.
        // If this differs from Position, the malus from PositionSkillEffects applies.
        public Position? AssignedPosition { get; set; }

        [Ignore]
        public Position EffectivePosition => AssignedPosition ?? Position;

        [Ignore]
        public string ShortPositionName => PositionDisplay.Short(Position);

        private List<PositionSkill>? _secondaryPositionsCache;
        private string _secondaryPositionsRaw = "[]";

        // Persisted as JSON since sqlite-net can't store complex lists directly.
        public string SecondaryPositionsRaw
        {
            get => _secondaryPositionsRaw;
            set { _secondaryPositionsRaw = value; _secondaryPositionsCache = null; }
        }

        // Positions besides the natural one where the player can play with a smaller
        // malus (depending on proficiency). Positions not listed get a much higher malus.
        // Cached since this is read repeatedly (e.g. once per lineup slot on every
        // drag/drop rebuild) and the JSON rarely changes.
        [Ignore]
        public List<PositionSkill> SecondaryPositions
        {
            get => _secondaryPositionsCache ??= JsonSerializer.Deserialize<List<PositionSkill>>(SecondaryPositionsRaw) ?? [];
            set { SecondaryPositionsRaw = JsonSerializer.Serialize(value); _secondaryPositionsCache = value; }
        }
        public double Rating { get; set; }
        public int Moral { get; set; }
        public double Size { get; set; }
        public int Fitness { get; set; }

        // Base fitness / "Grundfitness" (1-99): higher means less fatigue accumulated during
        // a match (see Match.DecayFitness) and faster recovery afterwards (see
        // MatchDayService.RegenerateFitness), though recovery always takes at least
        // MatchDayService.MinFitnessRecoveryDays regardless of this value.
        public int BaseFitness { get; set; }

        // Game date of this player's last appearance (minutes played > 0) in any competition.
        // Drives day-by-day fitness regeneration in MatchDayService.RegenerateFitness. Null =
        // never played (or not tracked yet on an older save).
        public DateTime? LastMatchDate { get; set; }
        public int OffensivePower { get; set; }
        public int DefensivePower { get; set; }
        public int GameIntelligence { get; set; }
        public int PressingIntensity { get; set; }
        public int CounterSpeed { get; set; }
        public int PassingAccuracy { get; set; }
        public int DuelHardness { get; set; }
        public int DuelEfficiency { get; set; }
        public int CrossingAccuracy { get; set; }

        // Goalkeeper-specific attributes (only meaningful for Position.Goalkeeper). Outfield
        // players keep these at 0 - they are never read for outfield strength/duel calculations.
        public int GkReflexes { get; set; }
        public int GkHandling { get; set; }
        public int GkOneOnOne { get; set; }
        public int GkDistribution { get; set; }
        public int GkAerialControl { get; set; }

        // Outfield-only attributes (0 for goalkeepers, mirroring the Gk* fields above).
        // HeaderStrength/Jumping combine with Size for aerial duels (headed goals/clearances);
        // Dribbling/LongShot for midfield ball-carrying and shots from distance;
        // PenaltyKick/FreeKick for set pieces (see Match.cs).
        public int HeaderStrength { get; set; }
        public int Jumping { get; set; }
        public int Dribbling { get; set; }
        public int LongShotAccuracy { get; set; }
        public int PenaltyKick { get; set; }
        public int FreeKick { get; set; }

        // Finishing (clinical composure in front of goal, distinct from raw OffensivePower).
        public int Finishing { get; set; }

        // Positioning (reading the game, holding shape - key for defensive midfielders).
        public int Positioning { get; set; }
        public Personality Personality { get; set; }
        public PlayerStatus Status { get; set; }

        // Remaining matches of an active red-card ban (0 = none). Scoped to
        // SuspensionCompetition and only decremented by MatchDayService.RecoverForMatch when
        // preparing for a match in that same competition - friendlies never serve or block it,
        // and a ban from one competition doesn't block another (see Match.IssueRedCard).
        public int SuspensionMatchesRemaining { get; set; }

        // Competition the active suspension applies to. null = league (same convention as
        // MatchDayService.PersistPlayerStatsAsync, where null also means league).
        public CompetitionType? SuspensionCompetition { get; set; }

        // Only relevant when Status == Injured: date from which the player is available again.
        // null = no injury / duration not yet known.
        public DateTime? InjuredUntil { get; set; }

        // null = falls back to the team setting (Team.TacklingIntensity). Can be set at any
        // time, even during a running match (e.g. "play carefully, already has a yellow").
        public TacklingIntensity? TacklingIntensity { get; set; }

        // Calculates the age on a given game date from the date of birth.
        public int AgeOn(DateTime date)
        {
            int age = date.Year - DateOfBirth.Year;
            if (date < DateOfBirth.AddYears(age))
                age--;
            return age;
        }
    }
}
