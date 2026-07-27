using System;
using System.Collections.Generic;
using System.Text;

namespace RetroFootballManager.Models
{
    public class MatchStats
    {
        public int Goals { get; set; }

        // Shots
        public int Shots { get; set; }
        public int ShotsOnTarget { get; set; }

        // Possession (%)
        public int Possession { get; set; }

        // Pass accuracy (%)
        public int PassAccuracy { get; set; }

        // Tackles
        public int Tackles { get; set; }
        public int TacklesWon { get; set; }

        // Fouls & cards
        public int Fouls { get; set; }
        public int YellowCards { get; set; }
        public int RedCards { get; set; }

        public int YellowRedCards { get; set; }

        // Set pieces
        public int Corners { get; set; }
        public int FreeKicks { get; set; }
        public int Penaltys { get; set; }
    }
}
