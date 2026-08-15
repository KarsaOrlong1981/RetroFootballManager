using SQLite;

namespace RetroFootballManager.Models
{
    public class TeamStats
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int TeamId { get; set; }

        public int Season { get; set; }

        // League performance
        public int MatchesPlayed { get; set; }
        public int Wins { get; set; }
        public int Draws { get; set; }
        public int Losses { get; set; }

        // Goals
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        [Ignore]
        public int GoalDifference => GoalsFor - GoalsAgainst;

        // Points
        [Ignore]
        public int Points => (Wins * 3) + (Draws * 1);

        // Form (retro style: last 5 matches), persisted as "WWDLW"
        public string FormRaw { get; set; } = string.Empty;

        [Ignore]
        public List<char> Form
        {
            get => FormRaw.ToList();
            set => FormRaw = new string(value.ToArray());
        }
        // 'W' = Win, 'D' = Draw, 'L' = Loss

        // Discipline
        public int YellowCards { get; set; }
        public int RedCards { get; set; }
        public int YellowRedCards { get; set; }
        public int Shots { get; set; }
        public int ShotsOnTarget { get; set; }
        public int Corners { get; set; }
        public int FreeKicks { get; set; }
        public int Penaltys { get; set; }
        public int Offsides { get; set; }
        public int Passes { get; set; }
        public int SuccessfulPasses { get; set; }

        [Ignore]
        public double AverageCorners => MatchesPlayed == 0 ? 0 : (double)Corners / MatchesPlayed;
        [Ignore]
        public double AverageFreeKicks => MatchesPlayed == 0 ? 0 : (double)FreeKicks / MatchesPlayed;
        [Ignore]
        public double AveragePenaltys => MatchesPlayed == 0 ? 0 : (double)Penaltys / MatchesPlayed;
        [Ignore]
        public double AverageOffsides => MatchesPlayed == 0 ? 0 : (double)Offsides / MatchesPlayed;

        // Possession (%), persisted comma-separated
        public string PossessionsRaw { get; set; } = string.Empty;

        [Ignore]
        public List<int> Possessions
        {
            get => ParseInts(PossessionsRaw);
            set => PossessionsRaw = string.Join(',', value);
        }

        [Ignore]
        public int AveragePossessions => Possessions.Count == 0 ? 0 : (int)Math.Round(Possessions.Average());

        // Pass accuracy (%), persisted comma-separated
        public string PassAccuracysRaw { get; set; } = string.Empty;

        [Ignore]
        public List<int> PassAccuracys
        {
            get => ParseInts(PassAccuracysRaw);
            set => PassAccuracysRaw = string.Join(',', value);
        }

        [Ignore]
        public int AveragePassAccuracy => PassAccuracys.Count == 0 ? 0 : (int)Math.Round(PassAccuracys.Average());

        // Duels
        public int Tackles { get; set; }
        public int TacklesWon { get; set; }

        // Fouls & cards
        public int Fouls { get; set; }

        // Home/Away performance
        public int HomePoints { get; set; }
        public int AwayPoints { get; set; }

        // Temporary morale boosts (e.g. team meeting, psychologist, bonus)
        public int MoraleBoost { get; set; }

        // Weekly-recomputed (not cumulative like MoraleBoost) bonus from Physiotherapist/
        // MedicalStaff quality - see MatchDayService.ApplyPhysioMoraleBoost. Overwritten every
        // week rather than added to, so it self-corrects if staff are fired and never grows
        // unbounded.
        public int PhysioMoraleBoost { get; set; }

        // Same reasoning/pattern as PhysioMoraleBoost, for Psychologist/Motivation (previously
        // dead - PsychologistSession() below existed but was never called anywhere, and its
        // cumulative MoraleBoost += 8 would grow unbounded if invoked every week since
        // DecayBoosts() is never called either). See MatchDayService.ApplyPsychologistMoraleBoost.
        public int PsychologistMoraleBoost { get; set; }

        // Final morale (auto-calculated)
        [Ignore]
        public int Morale => Math.Clamp(CalculateMorale() + MoraleBoost + PhysioMoraleBoost + PsychologistMoraleBoost, 0, 100);

        private static List<int> ParseInts(string raw) =>
            raw.Length == 0
                ? []
                : raw.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();

        private int CalculateMorale()
        {
            var form = Form;
            if (form.Count == 0)
                return 50;

            int wins = form.Count(f => f == 'W');
            int draws = form.Count(f => f == 'D');
            int losses = form.Count(f => f == 'L');

            int morale = 50
                         + (wins * 10)
                         + (draws * 2)
                         - (losses * 8);

            return morale;
        }

        public void AddPossession(int value)
        {
            var list = Possessions;
            list.Add(value);
            Possessions = list;
        }

        public void AddPassAccuracy(int value)
        {
            var list = PassAccuracys;
            list.Add(value);
            PassAccuracys = list;
        }

        // Records a match result into the form (keeps only the last 5)
        public void RecordResult(char result)
        {
            var form = Form;
            form.Add(result);
            if (form.Count > 5)
                form.RemoveAt(0);
            Form = form;
        }

        // Methods to rebuild morale
        public void TeamMeeting() => MoraleBoost += 5;
        public void PsychologistSession() => MoraleBoost += 8;
        public void BonusPayment() => MoraleBoost += 10;
        public void FanEvent() => MoraleBoost += 6;
        public void TrainingCamp() => MoraleBoost += 12;

        // Reset boosts (e.g. after matchday)
        public void DecayBoosts()
        {
            MoraleBoost = Math.Max(0, MoraleBoost - 5); // retro-style decay
        }
    }
}
