using System.ComponentModel;
using System.Text.Json;
using RetroFootballManager.Core.Models;
using SQLite;

namespace RetroFootballManager.Models
{
    public class Player : INotifyPropertyChanged
    {
        // Only CurrentTrainingFocus raises this - it's the one property the UI displays
        // live while the same Player instance stays bound (CollectionView recycling skips
        // rebinding on reference-equal BindingContext, so without this the training-focus
        // label never refreshes after a save).
        public event PropertyChangedEventHandler? PropertyChanged;

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

        private TrainableAttribute? _currentTrainingFocus;

        // Currently trained attribute - a deliberate choice (not repeatedly clickable) that
        // slowly brings progress over many weeks (see TrainingService).
        // null = no individual training focus set.
        public TrainableAttribute? CurrentTrainingFocus
        {
            get => _currentTrainingFocus;
            set
            {
                if (_currentTrainingFocus == value)
                    return;
                _currentTrainingFocus = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentTrainingFocus)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrainableLabel)));
            }
        }

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

        // Talk-to-player state (see ConversationService). TalkMotivationBoost is a temporary,
        // morale-independent training multiplier that decays weekly; RecentTalkStreak tracks
        // consecutive Praise/Criticize talks (positive = praise streak, negative = criticism
        // streak) to detect overpraise/overcriticism tipping points; RecentPersonalTalkStreak
        // counts consecutive personal (Neutral) talks the same way, to detect when small talk
        // becomes overfamiliar; WantsToLeaveClub is a warning flag (no automatic transfer
        // listing yet); LastTalkDate enforces a one-talk-per-week cooldown per player.
        public double TalkMotivationBoost { get; set; }
        public int RecentTalkStreak { get; set; }
        public int RecentPersonalTalkStreak { get; set; }
        public bool WantsToLeaveClub { get; set; }
        public DateTime? LastTalkDate { get; set; }
        public double Size { get; set; }
        public int Fitness { get; set; }
        public string? ImagePath { get; set; }
        public int BaseFitness { get; set; }
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

        // Transient in-match temperament (see InMatchCharacterEffects) - one of 15 fixed
        // types, rolled once at generation and backfilled for existing saves (nullable only
        // for that backward-compatibility reason, not "no character"). Distinct from the
        // permanent Personality above.
        public InMatchCharacterType? InMatchCharacter { get; set; }

        // In-match morale (0-100), seeded from Moral at kickoff and only changed in-memory
        // during the match (goals, half-time reactions, team talks) - never written back to
        // the persistent Moral field. Defaults to the neutral midpoint (not 0!) so
        // TeamStrengthCalculator's InMatchCharacterEffects.AttributeFactor is exactly 1.0
        // (no-op) for any player outside a live match - Calculate() is also used for
        // scouting/transfers/formation-scoring, not just Match.cs.
        [Ignore]
        public int InMatchMoral { get; set; } = 50;

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

        [Ignore]
        public string TrainableLabel => GetTrainableLabelString(CurrentTrainingFocus ?? TrainableAttribute.None);

        private static string GetTrainableLabelString(TrainableAttribute attribute) => attribute switch
        {
            TrainableAttribute.Offensive => "Offensivkraft",
            TrainableAttribute.Defensive => "Defensivkraft",
            TrainableAttribute.GameIntelligence => "Spielintelligenz",
            TrainableAttribute.Pressing => "Pressing",
            TrainableAttribute.CounterSpeed => "Kontertempo",
            TrainableAttribute.Passing => "Passgenauigkeit",
            TrainableAttribute.DuelHardness => "Zweikampfhärte",
            TrainableAttribute.DuelEfficiency => "Zweikampfeffizienz",
            TrainableAttribute.Crossing => "Flanken",
            TrainableAttribute.GkReflexes => "Reflexe",
            TrainableAttribute.GkHandling => "Ballsicherheit",
            TrainableAttribute.GkOneOnOne => "Eins-gegen-eins",
            TrainableAttribute.GkDistribution => "Spieleröffnung",
            TrainableAttribute.GkAerialControl => "Herauslaufen/Flanken",
            TrainableAttribute.HeaderStrength => "Kopfballstärke",
            TrainableAttribute.Jumping => "Sprungkraft",
            TrainableAttribute.Dribbling => "Dribbling",
            TrainableAttribute.LongShot => "Weitschuss",
            TrainableAttribute.PenaltyKick => "Elfmeterstärke",
            TrainableAttribute.FreeKick => "Freistoßstärke",
            TrainableAttribute.Finishing => "Abschluss",
            TrainableAttribute.Positioning => "Stellungsspiel",
            TrainableAttribute.None => "Kein Fokus",
            _ => attribute.ToString(),
        };
    }
}
