using SQLite;

namespace RetroFootballManager.Models
{
    // A tie in a KO/group competition (German Cup, Champions League, Europa Cup - all three
    // share this one table). Round progression is derived arithmetically from
    // MatchNumberInRound (see CupDrawService), no "NextMatchNumber" column needed.
    public class CupTie
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public CompetitionType CompetitionType { get; set; }

        [Indexed]
        public int Season { get; set; }

        [Indexed]
        public int Round { get; set; }

        public int MatchNumberInRound { get; set; }

        // Group ID ("A".."H") - only set during the group stage (M6c/M6d).
        public string? Group { get; set; }

        [Indexed]
        public int HomeTeamId { get; set; }

        [Indexed]
        public int AwayTeamId { get; set; }

        public DateTime Date { get; set; }
        public bool Played { get; set; }
        public int HomeGoals { get; set; }
        public int AwayGoals { get; set; }

        public const int LegNone = 0;
        public const int LegFirst = 1;
        public const int LegSecond = 2;

        // 0 = no two-legged tie (group stage, all German Cup rounds, CL/EC final);
        // 1 = first leg, 2 = second leg (round of 16/quarter-final/semi-final of Champions
        // League/Europa Cup) - pairing found via matching Round+MatchNumberInRound, no FK needed.
        public int LegNumber { get; set; }

        public bool WentToPenalties { get; set; }
        public int? PenaltyHomeGoals { get; set; }
        public int? PenaltyAwayGoals { get; set; }

        // Prevents duplicate analyst preview messages when the main menu is visited multiple
        // times before the same due match (see PreMatchAnalysisService/MainMenuViewModel).
        public bool AnalysisSent { get; set; }

        // Bye on an odd number of participants (see CupDrawService.BuildNextRound) - AwayTeamId
        // stays 0 (never a real team ID), the actual marker is IsBye.
        public bool IsBye { get; set; }

        [Ignore]
        public int WinnerTeamId => WentToPenalties
            ? (PenaltyHomeGoals > PenaltyAwayGoals ? HomeTeamId : AwayTeamId)
            : (HomeGoals > AwayGoals ? HomeTeamId : AwayTeamId);
    }
}
