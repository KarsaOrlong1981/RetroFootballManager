using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public class AiMatchCoach : IMatchCoach
    {
        // Note: fitness decay is currently mild 1 per 5 min, so "tired" is calibrated to
        // that model - by the last 15 minutes the most-used players drop below this and get
        // rotated. A richer fatigue model is a later refinement.
        private const int TiredFitnessThreshold = 85;
        private const int TiredSubEarliestMinute = 60;
        private const int MinMinutesBetweenSubs = 6;

        private TacticalOrientation? _baseOrientation;
        private readonly HashSet<int> _handledInjuries = [];
        private int _lastSubMinute = -100;
        private bool _hasGivenTeamTalk;

        // Goal difference at/above this counts as a comfortable lead for the team-talk
        // heuristic below (same threshold as ApplyHalfTimeCharacterEffects's "big lead").
        private const int ComfortableLeadGoalDiff = 2;

        public void OnMinute(Match match, bool isHome, int minute)
        {
            var team = isHome ? match.HomeTeam : match.AwayTeam;
            _baseOrientation ??= team.TacticalOrientation;

            AdjustOrientation(match, isHome, minute);
            HandleInjuries(match, isHome, minute);
            HandleTiredSubs(match, isHome, minute);
            GiveAutomaticTeamTalk(match, isHome);
        }

        // Gives a half-time team talk automatically for COM-controlled teams (no UI needed) -
        // triggered once, on the first OnMinute call after the second half kicks off, so it
        // also works in pure AI-vs-AI matches (e.g. a season simulation harness). Simple
        // scoreline heuristic: behind -> cheer the team on, comfortably ahead -> warn against
        // complacency, otherwise a neutral tactical word.
        private void GiveAutomaticTeamTalk(Match match, bool isHome)
        {
            if (_hasGivenTeamTalk || match.Phase != MatchPhase.SecondHalf)
                return;

            _hasGivenTeamTalk = true;

            int goalDiff = isHome ? match.HomeGoals - match.AwayGoals : match.AwayGoals - match.HomeGoals;
            var option = goalDiff < 0 ? TeamTalkOption.CheerOn
                : goalDiff >= ComfortableLeadGoalDiff ? TeamTalkOption.WarnAgainstComplacency
                : TeamTalkOption.TacticalTalk;

            TeamTalkService.TryApply(match, isHome, option);
        }

        // Reacts to the scoreline by dialing the orientation more offensive/defensive - the
        // playing style itself is a manual choice the AI does not change reactively.
        private void AdjustOrientation(Match match, bool isHome, int minute)
        {
            if (minute < 45)
                return;

            int goalDiff = isHome
                ? match.HomeGoals - match.AwayGoals
                : match.AwayGoals - match.HomeGoals;

            var target = _baseOrientation!.Value;
            if (goalDiff <= -2)
                target = TacticalOrientation.VeryOffensive;
            else if (goalDiff == -1 && minute >= 65)
                target = TacticalOrientation.Offensive;
            else if (goalDiff >= 2 && minute >= 75)
                target = TacticalOrientation.VeryDefensive;

            // SetOrientation ist ein No-Op, if nothing changes
            match.SetOrientation(isHome, target);
        }

        private void HandleInjuries(Match match, bool isHome, int minute)
        {
            var team = isHome ? match.HomeTeam : match.AwayTeam;

            foreach (var injured in team.Players.Where(p => p.Status == PlayerStatus.Injured))
            {
                if (_handledInjuries.Contains(injured.Id))
                    continue;

                _handledInjuries.Add(injured.Id);

                if (match.SubsRemaining(isHome) <= 0)
                    continue;

                var replacement = LineupSelector.BestForPosition(match.Bench(isHome), injured.EffectivePosition);
                if (replacement is not null && match.TrySubstitute(isHome, injured, replacement))
                    _lastSubMinute = minute;
            }
        }

        private void HandleTiredSubs(Match match, bool isHome, int minute)
        {
            if (minute < TiredSubEarliestMinute)
                return;
            if (match.SubsRemaining(isHome) <= 0)
                return;
            if (minute - _lastSubMinute < MinMinutesBetweenSubs)
                return;

            // search trierd player
            var tired = match.OnPitch(isHome)
                .Where(p => p.EffectivePosition != Position.Goalkeeper && p.Fitness < TiredFitnessThreshold)
                .OrderBy(p => p.Fitness)
                .FirstOrDefault();

            if (tired is null)
                return;

            var replacement = LineupSelector.BestForPosition(match.Bench(isHome), tired.EffectivePosition);
            if (replacement is not null && match.TrySubstitute(isHome, tired, replacement))
                _lastSubMinute = minute;
        }
    }
}
