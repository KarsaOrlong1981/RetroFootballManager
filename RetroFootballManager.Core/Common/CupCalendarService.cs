using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Fixed season calendar for all three competitions: week + own weekday from season
    // start, so league (Sat/Sun) and German Cup (Tue) / Champions League (Wed) /
    // Europa Cup (Thu) never overlap and rounds are spread across the season instead
    // of clustering (modeled on real schedules: cup round of 16 after the winter break
    // in February, tournament finals after season end in May).
    public static class CupCalendarService
    {
        // Weeks from season start for the German Cup rounds.
        private static readonly Dictionary<int, int> GermanCupWeekByRound = new()
        {
            [CupDrawService.RoundPreliminary] = 1,
            [CupDrawService.RoundLastSixtyFour] = 6,
            [CupDrawService.RoundLastThirtyTwo] = 13,
            [CupDrawService.RoundLastSixteen] = 24,
            [CupDrawService.RoundQuarterFinal] = 27,
            [CupDrawService.RoundSemiFinal] = 36,
            [CupDrawService.RoundFinal] = 41,
        };

        private static readonly Dictionary<int, int> ChampionsLeagueKoWeek = new()
        {
            [CupDrawService.RoundLastSixteen] = 27,
            [CupDrawService.RoundQuarterFinal] = 34,
            [CupDrawService.RoundSemiFinal] = 37,
            [CupDrawService.RoundFinal] = 42,
        };

        // Europa Cup final one week before the Champions League final (like the real
        // UEL final before the UCL final).
        private static readonly Dictionary<int, int> EuropaCupKoWeek = new()
        {
            [CupDrawService.RoundLastSixteen] = 27,
            [CupDrawService.RoundQuarterFinal] = 34,
            [CupDrawService.RoundSemiFinal] = 37,
            [CupDrawService.RoundFinal] = 41,
        };

        // Weeks of the 6 group matchdays (September to November, every 2 weeks).
        private static readonly int[] GroupMatchdayWeeks = [6, 8, 10, 12, 14, 16];

        // secondLeg pushes the second leg back one week - the gaps between CL/Europa Cup
        // knockout round weeks are always >=3 weeks, so +1 week never collides with the
        // next round.
        public static DateTime GetKoRoundDate(CompetitionType competition, int round, DateTime seasonStart, bool secondLeg = false)
        {
            int week = competition switch
            {
                CompetitionType.GermanCup => GermanCupWeekByRound[round],
                CompetitionType.ChampionsLeague => ChampionsLeagueKoWeek[round],
                _ => EuropaCupKoWeek[round],
            };
            if (secondLeg)
                week += 1;

            return WeekdayFor(competition, seasonStart, week);
        }

        public static DateTime GetGroupMatchdayDate(CompetitionType competition, int matchdayIndex, DateTime seasonStart) =>
            WeekdayFor(competition, seasonStart, GroupMatchdayWeeks[matchdayIndex]);

        public static int GroupMatchdayCount => GroupMatchdayWeeks.Length;

        private static DateTime WeekdayFor(CompetitionType competition, DateTime seasonStart, int week)
        {
            var day = competition switch
            {
                CompetitionType.GermanCup => DayOfWeek.Tuesday,
                CompetitionType.ChampionsLeague => DayOfWeek.Wednesday,
                _ => DayOfWeek.Thursday,
            };

            var candidate = seasonStart.Date.AddDays(week * 7);
            int offset = ((int)day - (int)candidate.DayOfWeek + 7) % 7;
            return candidate.AddDays(offset);
        }
    }
}
