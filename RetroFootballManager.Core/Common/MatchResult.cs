using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Final result of a simulated match, ready to be applied to team/player statistics.
    public class MatchResult
    {
        public int HomeGoals { get; set; }
        public int AwayGoals { get; set; }

        public MatchStats MatchStatsHome { get; set; } = new();
        public MatchStats MatchStatsAway { get; set; } = new();

        public List<Player> HomeScorers { get; } = [];
        public List<Player> AwayScorers { get; } = [];
        public List<Player> HomeYellowCards { get; } = [];
        public List<Player> AwayYellowCards { get; } = [];
        public List<Player> HomeRedCards { get; } = [];
        public List<Player> AwayRedCards { get; } = [];
        public List<Player> InjuredPlayers { get; } = [];

        // Ban length (in matches) per sent-off player, rolled at the moment of the red card -
        // 3 for a straight red, 1 for a second-yellow (see Match.IssueRedCard). Applied via
        // ApplySuspensions once the caller knows the competition (league/cup); never applied
        // for friendlies, which don't hand out suspensions at all.
        public Dictionary<int, int> SuspensionMatchesByPlayerId { get; } = [];

        // Injury duration (in days) per injured player (PlayerId -> days), rolled at the moment
        // of injury during the match. The match itself has no real calendar date, so only the
        // duration is stored here - ApplyInjuryDurations converts it to an absolute
        // Player.InjuredUntil using the actual match date.
        public Dictionary<int, int> InjuryDurationDays { get; } = [];

        public List<MatchEvent> Events { get; } = [];

        // Match statistics per player (Id -> stats for this one match).
        public Dictionary<int, PlayerStats> PlayerMatchStats { get; } = [];

        // Minutes played per player (Id -> minutes on the pitch) - basis for playtime-dependent
        // player development (mainly youth players).
        public Dictionary<int, int> MinutesPlayed { get; } = [];

        // Minutes each side spent under an Offensive/VeryOffensive resp. Defensive/
        // VeryDefensive TacticalOrientation - basis for ManagerGrowthService's Offensive
        // Creation/Defensive Organization skill growth (a match "qualifies" once a side
        // spent enough of it playing that way, see ManagerGrowthService.OrientationMinuteThreshold).
        public int HomeOffensiveOrientationMinutes { get; set; }
        public int HomeDefensiveOrientationMinutes { get; set; }
        public int AwayOffensiveOrientationMinutes { get; set; }
        public int AwayDefensiveOrientationMinutes { get; set; }

        public char HomeResult => HomeGoals > AwayGoals ? 'W' : HomeGoals < AwayGoals ? 'L' : 'D';
        public char AwayResult => AwayGoals > HomeGoals ? 'W' : AwayGoals < HomeGoals ? 'L' : 'D';

        // Applies the result to both teams' season/team statistics.
        public void ApplyToTeamStats(TeamStats home, TeamStats away)
        {
            home.MatchesPlayed++;
            away.MatchesPlayed++;

            home.GoalsFor += HomeGoals;
            home.GoalsAgainst += AwayGoals;
            away.GoalsFor += AwayGoals;
            away.GoalsAgainst += HomeGoals;

            switch (HomeResult)
            {
                case 'W': home.Wins++; home.HomePoints += 3; away.Losses++; break;
                case 'L': home.Losses++; away.Wins++; away.AwayPoints += 3; break;
                default: home.Draws++; away.Draws++; home.HomePoints += 1; away.AwayPoints += 1; break;
            }

            home.RecordResult(HomeResult);
            away.RecordResult(AwayResult);

            home.Shots += MatchStatsHome.Shots;
            home.ShotsOnTarget += MatchStatsHome.ShotsOnTarget;
            home.Fouls += MatchStatsHome.Fouls;
            home.YellowCards += MatchStatsHome.YellowCards;
            home.RedCards += MatchStatsHome.RedCards;
            home.Corners += MatchStatsHome.Corners;
            home.FreeKicks += MatchStatsHome.FreeKicks;
            home.Penaltys += MatchStatsHome.Penaltys;
            home.Offsides += MatchStatsHome.Offsides;
            home.Passes += MatchStatsHome.Passes;
            home.SuccessfulPasses += MatchStatsHome.SuccessfulPasses;
            home.AddPossession(MatchStatsHome.Possession);
            home.AddPassAccuracy(MatchStatsHome.PassAccuracy);

            away.Shots += MatchStatsAway.Shots;
            away.ShotsOnTarget += MatchStatsAway.ShotsOnTarget;
            away.Fouls += MatchStatsAway.Fouls;
            away.YellowCards += MatchStatsAway.YellowCards;
            away.RedCards += MatchStatsAway.RedCards;
            away.Corners += MatchStatsAway.Corners;
            away.FreeKicks += MatchStatsAway.FreeKicks;
            away.Penaltys += MatchStatsAway.Penaltys;
            away.Offsides += MatchStatsAway.Offsides;
            away.Passes += MatchStatsAway.Passes;
            away.SuccessfulPasses += MatchStatsAway.SuccessfulPasses;
            away.AddPossession(MatchStatsAway.Possession);
            away.AddPassAccuracy(MatchStatsAway.PassAccuracy);
        }

        // Sets Player.InjuredUntil based on the actual match date + rolled injury duration -
        // separate from ApplyToTeamStats because the match itself has no calendar date.
        public void ApplyInjuryDurations(DateTime matchDate)
        {
            foreach (var player in InjuredPlayers)
            {
                if (InjuryDurationDays.TryGetValue(player.Id, out int days))
                    player.InjuredUntil = matchDate.AddDays(days);
            }
        }

        // Starts serving any red-card bans from this match in the given competition
        // (null = league). Not called for friendlies - a red card there never suspends.
        public void ApplySuspensions(CompetitionType? competition)
        {
            foreach (var player in HomeRedCards.Concat(AwayRedCards))
            {
                if (!SuspensionMatchesByPlayerId.TryGetValue(player.Id, out int matches))
                    continue;

                player.SuspensionMatchesRemaining = matches;
                player.SuspensionCompetition = competition;
                player.Status = PlayerStatus.Suspended;
            }
        }
    }
}
