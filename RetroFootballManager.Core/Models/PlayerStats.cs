using SQLite;

namespace RetroFootballManager.Models
{
    public class PlayerStats
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int PlayerId { get; set; }

        public int Season { get; set; }

        // null = league stats (default). Set = cup stats for this specific competition -
        // own row per (PlayerId, Season, Competition) combination.
        public CompetitionType? Competition { get; set; }

        // Basic actions
        public int Passes { get; set; }
        public int SuccessfulPasses { get; set; }
        public int Crosses { get; set; }
        public int SuccessfulCrosses { get; set; }
        public int Shots { get; set; }
        public int ShotsOnTarget { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int Offsides { get; set; }

        // Duels
        public int Tackles { get; set; }
        public int TacklesWon { get; set; }
        public int HeaderDuels { get; set; }
        public int HeaderDuelsWon { get; set; }

        // Ball actions
        public int Dribbles { get; set; }
        public int SuccessfulDribbles { get; set; }
        public int BallLosses { get; set; }
        public int BallRecoveries { get; set; }

        // Defensive actions
        public int Clearances { get; set; }        // clearances
        public int Interceptions { get; set; }     // intercepted passes
        public int Blocks { get; set; }            // blocked shots

        // Fouls & cards
        public int Fouls { get; set; }
        public int YellowCards { get; set; }
        public int RedCards { get; set; }

        // Goalkeeper-specific (optional)
        public int Saves { get; set; }             // saves
        public int GoalsConceded { get; set; }     // goals conceded
        public int CleanSheets { get; set; }       // matches with GoalsConceded == 0 (min. playing time)

        // Running / fitness
        public int DistanceCovered { get; set; }   // meters or km
        public int Stamina { get; set; }           // 0-100

        // Influence
        public int KeyPasses { get; set; }         // passes leading to chances
        public int ChancesCreated { get; set; }    // chances created

        // Rating
        public double Rating { get; set; }         // school grade: 1.0 (best) - 6.0 (worst)

        // Number of appearances these stats were collected over (for rating average)
        public int Appearances { get; set; }

        // Adds the values of a single match performance to these (career/season) stats
        public void AddMatchStats(PlayerStats matchStats)
        {
            Passes += matchStats.Passes;
            SuccessfulPasses += matchStats.SuccessfulPasses;
            Crosses += matchStats.Crosses;
            SuccessfulCrosses += matchStats.SuccessfulCrosses;
            Shots += matchStats.Shots;
            ShotsOnTarget += matchStats.ShotsOnTarget;
            Goals += matchStats.Goals;
            Assists += matchStats.Assists;
            Offsides += matchStats.Offsides;
            Tackles += matchStats.Tackles;
            TacklesWon += matchStats.TacklesWon;
            HeaderDuels += matchStats.HeaderDuels;
            HeaderDuelsWon += matchStats.HeaderDuelsWon;
            Dribbles += matchStats.Dribbles;
            SuccessfulDribbles += matchStats.SuccessfulDribbles;
            BallLosses += matchStats.BallLosses;
            BallRecoveries += matchStats.BallRecoveries;
            Clearances += matchStats.Clearances;
            Interceptions += matchStats.Interceptions;
            Blocks += matchStats.Blocks;
            Fouls += matchStats.Fouls;
            YellowCards += matchStats.YellowCards;
            RedCards += matchStats.RedCards;
            Saves += matchStats.Saves;
            GoalsConceded += matchStats.GoalsConceded;
            CleanSheets += matchStats.CleanSheets;
            DistanceCovered += matchStats.DistanceCovered;
            KeyPasses += matchStats.KeyPasses;
            ChancesCreated += matchStats.ChancesCreated;

            Rating = Appearances == 0
                ? matchStats.Rating
                : ((Rating * Appearances) + matchStats.Rating) / (Appearances + 1);
            Appearances += 1;
        }
    }
}
