using RetroFootballManager.Helper;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public enum MatchPhase
    {
        NotStarted,
        FirstHalf,
        HalfTime,
        SecondHalf,
        Finished
    }

    // Simulates a complete match minute by minute. Event probabilities are derived from
    // tactics, morale, player fitness, player personalities and (for the home team) the
    // stadium (TeamStrengthCalculator).
    //
    // The engine is controllable as a state machine: Begin() + repeated AdvanceMinute()
    // drive the match forward and stop at half-time, so a live view can pause, change speed
    // and intervene live (substitutions/tactics). Simulate() and SimulateAsync() remain as
    // full runs (tests, AI matches in the other leagues).
    public class Match
    {
        public const int MaxSubstitutions = 5;

        // Re-calibrated (see 2026-07-24 session): with the previous rates (0.075/0.55/0.45/
        // 0.40), a 10-matchday simulation across all 4 league tiers averaged only ~0.5-0.65
        // total goals/game with 50-63% of games ending 0:0 - in every tier, not just the
        // weakest one, since the whole formula chain is relative (ratios), not tier-sensitive.
        // Raised ChanceBaseRate/ShotConversionBase/GoalBase to land on realistic pro-football
        // per-team averages (~11 shots, ~5 on target, ~1.3-1.5 goals). PenaltyChance/
        // FreeKickChance are scaled down by the same factor as ChanceBaseRate so their
        // per-game frequency (rolled per dangerous attack) doesn't rise along with it.
        private const double ChanceBaseRate = 0.16;
        private const double ShotConversionBase = 0.72;
        private const double OnTargetBase = 0.45;
        private const double GoalBase = 0.62;
        // FoulBase calibrated to ~10-12 fouls/team/game (realistic football average).
        // Of these fouls, only ~0.8% turn into a direct red card (rough/reckless play) and
        // ~21% into yellow - previously the red rate was 10% per foul, which at realistic
        // foul counts led to a red card in almost every game.
        private const double FoulBase = 0.06;
        private const double RedCardShare = 0.0015;
        private const double YellowCardShare = 0.07;
        private const double InjuryBase = 0.0015;
        private const double SaveReboundCornerChance = 0.3;
        private const double PenaltyChance = 0.012;
        private const double PenaltyConversionBase = 0.78;
        private const double PenaltySavedGivenMissChance = 0.65;
        private const double PenaltyRedCardChance = 0.06;

        // Share of shots that are header attempts vs. long-range attempts (rest are normal
        // open-play shots) - see ResolveShotType/HeaderPower below.
        private const double HeaderAttemptShare = 0.22;
        private const double LongShotAttemptShare = 0.16;
        // Direct free-kick chance per dangerous attack (a foul just outside the box).
        private const double FreeKickChance = 0.006;
        private const double FreeKickConversionBase = 0.10;

        private readonly Team _homeTeam;
        private readonly Team _awayTeam;
        private readonly Random _random;

        private double _homePossessionSum;
        private double _awayPossessionSum;
        private double _homePassAccuracySum;
        private double _awayPassAccuracySum;
        private int _sampledMinutes;

        private MatchResult _result = new();
        private IProgress<MatchEvent>? _progress;
        private int _minute;
        private int _firstHalfEnd;
        private int _secondHalfEnd;
        private int _homeSubsUsed;
        private int _awaySubsUsed;

        public Match(Team homeTeam, Team awayTeam, Random? random = null)
        {
            _homeTeam = homeTeam;
            _awayTeam = awayTeam;
            _random = random ?? Random.Shared;
        }

        // Optional coach control per side (e.g. AI opponent). null = not auto-controlled
        // (human player intervenes via the UI).
        public IMatchCoach? HomeCoach { get; set; }
        public IMatchCoach? AwayCoach { get; set; }

        public MatchPhase Phase { get; private set; } = MatchPhase.NotStarted;
        public int CurrentMinute => _minute;
        public MatchResult Result => _result;
        public Team HomeTeam => _homeTeam;
        public Team AwayTeam => _awayTeam;
        public int HomeGoals => _result.HomeGoals;
        public int AwayGoals => _result.AwayGoals;
        public bool IsFinished => Phase == MatchPhase.Finished;

        public int SubsUsed(bool isHome) => isHome ? _homeSubsUsed : _awaySubsUsed;
        public int SubsRemaining(bool isHome) => MaxSubstitutions - SubsUsed(isHome);

        public IReadOnlyList<Player> OnPitch(bool isHome) =>
            TeamStrengthCalculator.GetLineup(isHome ? _homeTeam : _awayTeam);

        public IReadOnlyList<Player> Bench(bool isHome) =>
            (isHome ? _homeTeam : _awayTeam).Players
                .Where(p => p.Status == PlayerStatus.OnBench).ToList();

        // Simulates the complete match immediately (no real-time delay) - for logic/tests.
        public MatchResult Simulate(IProgress<MatchEvent>? progress = null)
        {
            Begin(progress);
            while (!IsFinished)
                AdvanceMinute();
            return _result;
        }

        // Like Simulate(), but with an artificial delay per minute - for UI playback.
        public async Task<MatchResult> SimulateAsync(
            IProgress<MatchEvent>? progress = null,
            int minuteDelayMs = 0,
            CancellationToken cancellationToken = default)
        {
            Begin(progress);
            while (!IsFinished)
            {
                bool simulated = AdvanceMinute();
                if (simulated && minuteDelayMs > 0)
                    await Task.Delay(minuteDelayMs, cancellationToken);
            }
            return _result;
        }

        // Starts the match: kick-off, roll stoppage time, phase = first half.
        public void Begin(IProgress<MatchEvent>? progress = null)
        {
            _result = new MatchResult();
            _progress = progress;
            _minute = 0;
            _homeSubsUsed = 0;
            _awaySubsUsed = 0;

            EmitEvent(_result, _progress, 0, GameEventType.KickOff, true, null, "Anpfiff");

            int extraTime1 = _random.Next(1, 5);
            int extraTime2 = _random.Next(1, 6);
            _firstHalfEnd = 45 + extraTime1;
            _secondHalfEnd = _firstHalfEnd + 45 + extraTime2;

            Phase = MatchPhase.FirstHalf;
        }

        // Advances the match by one minute (or switches from half-time break into the
        // second half). Returns true if a minute was actually played (false on half-time
        // resumption, so the UI doesn't delay twice).
        public bool AdvanceMinute()
        {
            if (Phase == MatchPhase.NotStarted)
                Begin(_progress);

            if (Phase == MatchPhase.HalfTime)
            {
                Phase = MatchPhase.SecondHalf;
            }

            if (Phase is not (MatchPhase.FirstHalf or MatchPhase.SecondHalf))
                return false;

            _minute++;
            SimulateMinute(_result, _progress, _minute);
            TrackMinutesPlayed();
            RunCoaches();

            if (Phase == MatchPhase.FirstHalf && _minute >= _firstHalfEnd)
            {
                EmitEvent(_result, _progress, _firstHalfEnd, GameEventType.HalfTime, true, null, "Halbzeit");
                Phase = MatchPhase.HalfTime;
            }
            else if (Phase == MatchPhase.SecondHalf && _minute >= _secondHalfEnd)
            {
                EmitEvent(_result, _progress, _secondHalfEnd, GameEventType.FullTime, true, null,
                    $"Abpfiff: {_homeTeam.Name} {_result.HomeGoals}:{_result.AwayGoals} {_awayTeam.Name}");
                FinalizePossessionAndPassing(_result);
                Phase = MatchPhase.Finished;
            }

            return true;
        }

        // Substitutes a player in. off must be on the pitch (or just gone off injured),
        // on must be from the bench; max MaxSubstitutions per team.
        public bool TrySubstitute(bool isHome, Player off, Player on)
        {
            var team = isHome ? _homeTeam : _awayTeam;

            if (SubsRemaining(isHome) <= 0 || off.Id == on.Id)
                return false;
            if (!team.Players.Contains(off) || !team.Players.Contains(on))
                return false;
            if (on.Status != PlayerStatus.OnBench)
                return false;
            if (off.Status is not (PlayerStatus.InStartingXI or PlayerStatus.Injured))
                return false;

            on.Status = PlayerStatus.InStartingXI;
            on.AssignedPosition = off.EffectivePosition == on.Position ? null : off.EffectivePosition;
            // The substitute inherits the outgoing player's WB duty, if any, so the UI stays
            // consistent with what's actually simulated for the slot he just took over.
            on.UsedAsWingBack = off.EffectivePosition is Position.LeftWingBack or Position.RightWingBack;

            if (off.Status != PlayerStatus.Injured)
                off.Status = PlayerStatus.SubstitutedOff;

            if (isHome) _homeSubsUsed++;
            else _awaySubsUsed++;

            EmitEvent(_result, _progress, _minute, GameEventType.Substitution, isHome, on,
                $"Wechsel bei {team.Name}: {on.Name} kommt für {off.Name}.");
            return true;
        }

        // Changes the tactical orientation (e.g. AI opponent reacts to the score).
        public void SetOrientation(bool isHome, TacticalOrientation orientation)
        {
            var team = isHome ? _homeTeam : _awayTeam;
            if (team.TacticalOrientation == orientation)
                return;

            team.TacticalOrientation = orientation;
            EmitEvent(_result, _progress, _minute, GameEventType.TacticChange, isHome, null,
                $"{team.Name} stellt auf {OrientationName(orientation)} um.");
        }

        // Changes the playing style (manual adjustment by the player, e.g. in the
        // team management dialog; the AI only reactively changes the orientation).
        public void SetPlayingStyle(bool isHome, PlayingStyle style)
        {
            var team = isHome ? _homeTeam : _awayTeam;
            if (team.PlayingStyle == style)
                return;

            team.PlayingStyle = style;
            EmitEvent(_result, _progress, _minute, GameEventType.TacticChange, isHome, null,
                $"{team.Name} stellt auf {StyleName(style)} um.");
        }

        private void RunCoaches()
        {
            HomeCoach?.OnMinute(this, isHome: true, _minute);
            AwayCoach?.OnMinute(this, isHome: false, _minute);
        }

        private void TrackMinutesPlayed()
        {
            foreach (var p in TeamStrengthCalculator.GetLineup(_homeTeam))
                _result.MinutesPlayed[p.Id] = _result.MinutesPlayed.GetValueOrDefault(p.Id) + 1;
            foreach (var p in TeamStrengthCalculator.GetLineup(_awayTeam))
                _result.MinutesPlayed[p.Id] = _result.MinutesPlayed.GetValueOrDefault(p.Id) + 1;
        }

        private static string OrientationName(TacticalOrientation orientation) => orientation switch
        {
            TacticalOrientation.VeryDefensive => "Sehr Defensiv",
            TacticalOrientation.Defensive => "Defensiv",
            TacticalOrientation.Offensive => "Offensiv",
            TacticalOrientation.VeryOffensive => "Sehr Offensiv",
            _ => "Ausgeglichen",
        };

        private static string StyleName(PlayingStyle style) => style switch
        {
            PlayingStyle.CounterAttack => "Konter",
            PlayingStyle.TikiTaka => "Tiki-Taka",
            PlayingStyle.Pressing => "Pressing",
            PlayingStyle.WingPlay => "Flügelspiel",
            PlayingStyle.CrossesToStriker => "Flanken auf Stürmer",
            _ => style.ToString(),
        };

        private void SimulateMinute(MatchResult result, IProgress<MatchEvent>? progress, int minute)
        {
            var homeProfile = TeamStrengthCalculator.Calculate(_homeTeam, isHome: true);
            var awayProfile = TeamStrengthCalculator.Calculate(_awayTeam, isHome: false);

            RecordPossessionSample(homeProfile, awayProfile);

            ProcessAttack(result, progress, minute, _homeTeam, _awayTeam, homeProfile, awayProfile,
                isHomeAttacking: true, result.MatchStatsHome, result.MatchStatsAway);
            ProcessAttack(result, progress, minute, _awayTeam, _homeTeam, awayProfile, homeProfile,
                isHomeAttacking: false, result.MatchStatsAway, result.MatchStatsHome);

            ProcessDisciplineAndInjury(result, progress, minute, _homeTeam, homeProfile, isHome: true, result.MatchStatsHome);
            ProcessDisciplineAndInjury(result, progress, minute, _awayTeam, awayProfile, isHome: false, result.MatchStatsAway);

            DecayFitness(_homeTeam, minute);
            DecayFitness(_awayTeam, minute);
        }

        private void ProcessAttack(
            MatchResult result,
            IProgress<MatchEvent>? progress,
            int minute,
            Team attacking,
            Team defending,
            TeamStrengthProfile atk,
            TeamStrengthProfile def,
            bool isHomeAttacking,
            MatchStats attackingStats,
            MatchStats defendingStats)
        {
            double attackPower = (atk.Attack * 0.7) + (atk.Midfield * 0.3);
            double defensePower = (def.Defense * 0.7) + (def.Pressing * 0.3);
            double attackShare = attackPower / Math.Max(0.001, attackPower + defensePower);

            if (!Roll(ChanceBaseRate * (attackShare / 0.5)))
                return;

            EmitEvent(result, progress, minute, GameEventType.DangerousAttack, isHomeAttacking, null,
                EventTextHelper.DangerousAttackText(attacking, _random));

            // An aggressively defending team (high DuelHardnessFactor, e.g. defensive/
            // pressing, or a harsh TacklingIntensity setting) is more likely to commit
            // a penalty foul in the box.
            double defendingTeamAggression = defending.Tactic.DuelHardnessFactor
                * TacklingIntensityEffects.GetTeamAverageFoulCardRiskMultiplier(defending);
            if (Roll(PenaltyChance * defendingTeamAggression))
            {
                ResolvePenalty(result, progress, minute, attacking, defending, isHomeAttacking, attackingStats, defendingStats);
                return;
            }

            // A foul just outside the box - a direct free-kick chance, independent of the
            // regular shot path below.
            if (Roll(FreeKickChance * defendingTeamAggression))
            {
                ResolveFreeKick(result, progress, minute, attacking, defending, isHomeAttacking, attackingStats);
                return;
            }

            if (!Roll(ShotConversionBase * (attackShare / 0.5)))
                return;

            var lineup = TeamStrengthCalculator.GetLineup(attacking);

            // Decide the shot type first: header (needs a crosser), long-range, or normal -
            // each uses a different attribute for the shooter's finishing quality and a
            // different defensive counter (see below).
            bool isHeader = Roll(HeaderAttemptShare);
            bool isLongShot = !isHeader && Roll(LongShotAttemptShare / (1 - HeaderAttemptShare));

            Player? crosser = null;
            Player? shooter;
            if (isHeader)
            {
                crosser = PickWeightedPlayer(lineup, p => p.CrossingAccuracy * p.CrossingAccuracy);
                shooter = PickWeightedPlayer(lineup.Where(p => p.Id != crosser?.Id).ToList(),
                    p => HeaderPower(p) * (1 + PersonalityEffects.Get(p.Personality).AerialThreat - 1));
            }
            else if (isLongShot)
            {
                shooter = PickWeightedPlayer(lineup, p => p.LongShotAccuracy * p.LongShotAccuracy);
            }
            else
            {
                shooter = PickWeightedPlayer(lineup,
                    p => p.OffensivePower * (1 + PersonalityEffects.Get(p.Personality).AerialThreat - 1));
            }

            attackingStats.Shots++;
            GetOrCreateMatchStats(result, shooter).Shots++;
            EmitEvent(result, progress, minute, GameEventType.Shot, isHomeAttacking, shooter,
                shooter is not null ? EventTextHelper.ShotText(shooter, _random) : "Der Ball wird abgeschlossen.");

            if (!Roll(OnTargetBase * (attackShare / 0.5)))
                return;

            attackingStats.ShotsOnTarget++;
            GetOrCreateMatchStats(result, shooter).ShotsOnTarget++;
            EmitEvent(result, progress, minute, GameEventType.ShotOnTarget, isHomeAttacking, shooter,
                shooter is not null ? EventTextHelper.ShotOnTargetText(shooter, _random) : "Der Schuss geht aufs Tor.");

            var goalkeeper = TeamStrengthCalculator.GetGoalkeeper(defending);
            double buildUpRatio = attackPower / Math.Max(0.001, attackPower + (defensePower * 1.3));
            double goalProb;
            Player? preferredAssist = null;

            if (isHeader)
            {
                // Header goals: bigger, jumpier strikers with a good crosser feeding them beat
                // bigger, jumpier defenders and a commanding keeper. See "header goals should be
                // boosted by crossing play" - a strong crosser boosts the header
                // probability itself, not just the assist credit.
                double crossQuality = crosser is not null ? 0.7 + (crosser.CrossingAccuracy / 100.0 * 0.6) : 0.85;
                double headerPower = (shooter is not null ? HeaderPower(shooter) : attackPower) * crossQuality;
                var bestDefender = BestHeaderDefender(TeamStrengthCalculator.GetLineup(defending));
                double defenderHeaderPower = bestDefender is not null ? HeaderPower(bestDefender) : defensePower;
                double keeperAerial = goalkeeper?.GkAerialControl ?? defensePower;
                double headerDuelRatio = headerPower
                    / Math.Max(0.001, headerPower + (defenderHeaderPower * 1.1) + (keeperAerial * 0.5));
                goalProb = GoalBase * ((buildUpRatio * 0.4) + (headerDuelRatio * 0.6));
                preferredAssist = crosser;
            }
            else
            {
                // Besides overall team strength, a direct shooter-vs-keeper duel also matters:
                // a strong keeper saves more often, but a shooter with very good finishing
                // quality also prevails more often against a good keeper. The keeper's
                // reflexes/one-on-one count most here, DefensivePower/DuelEfficiency only
                // contribute with reduced weight.
                double shooterFinishing = isLongShot
                    ? shooter?.LongShotAccuracy ?? attackPower
                    : shooter?.OffensivePower ?? attackPower;
                double keeperReflexes = goalkeeper is not null
                    ? (goalkeeper.GkReflexes * 0.35) + (goalkeeper.GkOneOnOne * 0.25)
                      + (goalkeeper.DefensivePower * 0.2) + (goalkeeper.DuelEfficiency * 0.2)
                    : defensePower;
                double duelRatio = shooterFinishing / Math.Max(0.001, shooterFinishing + (keeperReflexes * 1.2));
                goalProb = GoalBase * ((buildUpRatio * 0.5) + (duelRatio * 0.5));
            }

            if (Roll(goalProb))
            {
                RegisterGoal(result, progress, minute, attacking, isHomeAttacking, shooter, attackingStats,
                    allowAssist: true, preferredAssist: preferredAssist);
            }
            else
            {
                EmitEvent(result, progress, minute, GameEventType.Save, !isHomeAttacking, goalkeeper,
                    EventTextHelper.SaveText(defending, _random));

                // Good handling (GkHandling) more often puts the ball safely out of play
                // instead of letting it rebound dangerously to an opponent's feet.
                double reboundCornerChance = goalkeeper is not null
                    ? Math.Clamp(SaveReboundCornerChance * (1.3 - (goalkeeper.GkHandling / 100.0)), 0.05, 0.9)
                    : SaveReboundCornerChance;
                if (Roll(reboundCornerChance))
                {
                    attackingStats.Corners++;
                    EmitEvent(result, progress, minute, GameEventType.Corner, isHomeAttacking, null,
                        EventTextHelper.CornerText(attacking, _random));
                }
            }
        }

        // Combines HeaderStrength with Jumping and Size (bigger/jumpier players win more
        // aerial duels) - used for both attacking header attempts and the defending side's
        // best aerial challenger.
        private static double HeaderPower(Player p) =>
            p.HeaderStrength * (0.6 + (0.25 * Math.Clamp(p.Jumping / 99.0, 0, 1))
                                     + (0.15 * Math.Clamp((p.Size - 1.65) / 0.35, 0, 1)));

        private static Player? BestHeaderDefender(List<Player> lineup) =>
            lineup.Where(p => p.EffectivePosition != Position.Goalkeeper)
                  .OrderByDescending(HeaderPower)
                  .FirstOrDefault();

        private void RegisterGoal(
            MatchResult result,
            IProgress<MatchEvent>? progress,
            int minute,
            Team attacking,
            bool isHomeAttacking,
            Player? shooter,
            MatchStats attackingStats,
            bool allowAssist,
            Player? preferredAssist = null)
        {
            attackingStats.Goals++;

            if (isHomeAttacking)
            {
                result.HomeGoals++;
                if (shooter is not null) result.HomeScorers.Add(shooter);
            }
            else
            {
                result.AwayGoals++;
                if (shooter is not null) result.AwayScorers.Add(shooter);
            }

            if (shooter is null)
                return;

            GetOrCreateMatchStats(result, shooter).Goals++;

            // Penalty goals arise from a foul, not an assist - so only look for an assist
            // provider on open-play goals. For header goals the assist provider is already
            // known (the crosser), instead of rolling again.
            Player? assistPlayer = allowAssist
                ? preferredAssist ?? EventTextHelper.PickAssistCandidate(shooter, attacking, _random)
                : null;
            if (assistPlayer is not null)
                GetOrCreateMatchStats(result, assistPlayer).Assists++;

            EmitEvent(result, progress, minute, GameEventType.Goal, isHomeAttacking, shooter,
                EventTextHelper.GoalText(shooter, attacking, assistPlayer, _random));
        }

        private void ResolvePenalty(
            MatchResult result,
            IProgress<MatchEvent>? progress,
            int minute,
            Team attacking,
            Team defending,
            bool isHomeAttacking,
            MatchStats attackingStats,
            MatchStats defendingStats)
        {
            var defendingLineup = TeamStrengthCalculator.GetLineup(defending);
            var fouler = PickWeightedPlayer(defendingLineup,
                p => p.DuelHardness * PersonalityEffects.Get(p.Personality).FoulChance
                   * TacklingIntensityEffects.GetFoulCardRiskMultiplier(p, defending));

            defendingStats.Fouls++;
            attackingStats.Penaltys++;
            if (fouler is not null)
                GetOrCreateMatchStats(result, fouler).Fouls++;

            EmitEvent(result, progress, minute, GameEventType.Penalty, isHomeAttacking, fouler,
                EventTextHelper.PenaltyAwardedText(attacking, fouler, _random));

            if (fouler is not null)
            {
                // From here we know the specific fouling player - their individual
                // TacklingIntensity (e.g. set live to "careful" because they already have
                // yellow) now determines the red-card chance, not the team setting anymore.
                double foulerAggression = defending.Tactic.DuelHardnessFactor
                    * TacklingIntensityEffects.GetFoulCardRiskMultiplier(fouler, defending);
                bool isRed = Roll(PenaltyRedCardChance * foulerAggression);
                ApplyFoulCard(result, progress, minute, fouler, !isHomeAttacking, defendingStats,
                    forceRed: isRed,
                    redTextFactory: r => EventTextHelper.RedCardProfessionalFoulText(fouler, r));
            }

            // Penalty takers are picked mainly by their PenaltyKick attribute (composure/
            // technique from the spot), not general finishing.
            var lineup = TeamStrengthCalculator.GetLineup(attacking);
            var taker = PickWeightedPlayer(lineup,
                p => (p.PenaltyKick * 0.7) + (p.OffensivePower * 0.2) + (p.GameIntelligence * 0.1));

            attackingStats.Shots++;
            GetOrCreateMatchStats(result, taker).Shots++;

            // The keeper also matters individually on a penalty - a strong keeper lowers the
            // otherwise very high base rate a bit, a strong taker raises it. One-on-one
            // strength counts most on a penalty (a pure duel on the line).
            var goalkeeper = TeamStrengthCalculator.GetGoalkeeper(defending);
            double takerComposure = taker?.PenaltyKick ?? 50;
            double keeperReflexes = goalkeeper is not null
                ? (goalkeeper.GkOneOnOne * 0.5) + (goalkeeper.GkReflexes * 0.3) + (goalkeeper.DuelEfficiency * 0.2)
                : 50;
            double conversionProb = PenaltyConversionProbability(takerComposure, keeperReflexes);

            if (Roll(conversionProb))
            {
                attackingStats.ShotsOnTarget++;
                GetOrCreateMatchStats(result, taker).ShotsOnTarget++;
                RegisterGoal(result, progress, minute, attacking, isHomeAttacking, taker, attackingStats, allowAssist: false);
            }
            else if (taker is not null && Roll(PenaltySavedGivenMissChance))
            {
                attackingStats.ShotsOnTarget++;
                GetOrCreateMatchStats(result, taker).ShotsOnTarget++;
                EmitEvent(result, progress, minute, GameEventType.Save, !isHomeAttacking, goalkeeper,
                    EventTextHelper.PenaltySavedText(defending, taker, _random));
            }
            else if (taker is not null)
            {
                EmitEvent(result, progress, minute, GameEventType.Shot, isHomeAttacking, taker,
                    EventTextHelper.PenaltyMissedText(taker, _random));
            }
        }

        // Extracted purely so the taker-vs-keeper duel math is directly unit-testable without
        // the noise of a full match simulation (open-play scoring dwarfs the penalty signal).
        public static double PenaltyConversionProbability(double takerComposure, double keeperReflexes)
        {
            double duelRatio = takerComposure / Math.Max(0.001, takerComposure + (keeperReflexes * 0.8));
            return Math.Clamp(PenaltyConversionBase + ((duelRatio - 0.55) * 0.4), 0.4, 0.95);
        }

        // A direct free kick from a foul just outside the box - much rarer and harder to
        // convert than a penalty, decided almost entirely by the taker's FreeKick attribute
        // versus the keeper's reflexes/handling (no defensive wall modelled).
        private void ResolveFreeKick(
            MatchResult result,
            IProgress<MatchEvent>? progress,
            int minute,
            Team attacking,
            Team defending,
            bool isHomeAttacking,
            MatchStats attackingStats)
        {
            var lineup = TeamStrengthCalculator.GetLineup(attacking);
            var taker = PickWeightedPlayer(lineup, p => (p.FreeKick * p.FreeKick) + 1);
            if (taker is null)
                return;

            attackingStats.Shots++;
            GetOrCreateMatchStats(result, taker).Shots++;
            EmitEvent(result, progress, minute, GameEventType.Shot, isHomeAttacking, taker,
                EventTextHelper.ShotText(taker, _random));

            var goalkeeper = TeamStrengthCalculator.GetGoalkeeper(defending);
            double keeperAbility = goalkeeper is not null
                ? (goalkeeper.GkReflexes * 0.5) + (goalkeeper.GkOneOnOne * 0.3) + (goalkeeper.GkHandling * 0.2)
                : 50;
            double duelRatio = taker.FreeKick / Math.Max(0.001, taker.FreeKick + keeperAbility);
            double conversionProb = Math.Clamp(FreeKickConversionBase + ((duelRatio - 0.5) * 0.5), 0.02, 0.45);

            if (Roll(conversionProb))
            {
                attackingStats.ShotsOnTarget++;
                GetOrCreateMatchStats(result, taker).ShotsOnTarget++;
                EmitEvent(result, progress, minute, GameEventType.ShotOnTarget, isHomeAttacking, taker,
                    EventTextHelper.ShotOnTargetText(taker, _random));
                RegisterGoal(result, progress, minute, attacking, isHomeAttacking, taker, attackingStats, allowAssist: false);
            }
            else
            {
                EmitEvent(result, progress, minute, GameEventType.Save, !isHomeAttacking, goalkeeper,
                    EventTextHelper.SaveText(defending, _random));
            }
        }

        private void ProcessDisciplineAndInjury(
            MatchResult result,
            IProgress<MatchEvent>? progress,
            int minute,
            Team team,
            TeamStrengthProfile profile,
            bool isHome,
            MatchStats matchStats)
        {
            var lineup = TeamStrengthCalculator.GetLineup(team);
            if (lineup.Count == 0)
                return;

            if (Roll(FoulBase * (1 + profile.DisciplineRisk)))
            {
                var fouler = PickWeightedPlayer(lineup,
                    p => p.DuelHardness * PersonalityEffects.Get(p.Personality).FoulChance
                       * TacklingIntensityEffects.GetFoulCardRiskMultiplier(p, team));

                matchStats.Fouls++;
                if (fouler is not null)
                    GetOrCreateMatchStats(result, fouler).Fouls++;

                EmitEvent(result, progress, minute, GameEventType.Foul, isHome, fouler,
                    fouler is not null ? EventTextHelper.FoulText(fouler, _random) : "Foul im Mittelfeld.");

                if (fouler is not null)
                {
                    // More aggressive tactics (high DuelHardnessFactor) and a harsh individual
                    // TacklingIntensity of the fouling player increase the chance of
                    // yellow/red for this specific foul.
                    double aggression = team.Tactic.DuelHardnessFactor
                        * TacklingIntensityEffects.GetFoulCardRiskMultiplier(fouler, team);
                    double cardRoll = _random.NextDouble();
                    if (cardRoll < RedCardShare * aggression)
                    {
                        // Foul was too hard -> straight red card, regardless of prior cards.
                        ApplyFoulCard(result, progress, minute, fouler, isHome, matchStats,
                            forceRed: true,
                            redTextFactory: r => EventTextHelper.RedCardHardFoulText(fouler, r));
                    }
                    else if (cardRoll < YellowCardShare * aggression)
                    {
                        ApplyFoulCard(result, progress, minute, fouler, isHome, matchStats,
                            forceRed: false,
                            redTextFactory: r => EventTextHelper.RedCardHardFoulText(fouler, r));
                    }
                }
            }

            double avgFitness = lineup.Average(p => p.Fitness);
            double conditionFactor = 1.5 - TeamStrengthCalculator.FitnessFactor((int)avgFitness);
            double stadiumConditionFactor = isHome && team.Stadium is not null
                ? 1.3 - (Math.Clamp(team.Stadium.Condition, 0, 100) / 100.0 * 0.3)
                : 1.0;

            if (Roll(InjuryBase * conditionFactor * stadiumConditionFactor))
            {
                var injured = lineup[_random.Next(lineup.Count)];
                injured.Status = PlayerStatus.Injured;
                result.InjuredPlayers.Add(injured);
                result.InjuryDurationDays[injured.Id] = ApplyMedicalStaffReduction(RollInjuryDurationDays(), team);
                EmitEvent(result, progress, minute, GameEventType.Injury, isHome, injured,
                    EventTextHelper.InjuryText(injured, _random));
            }
        }

        // Rolls the injury duration: mild (60%, 3-10 days), moderate (30%, 14-28 days),
        // severe (10%, 28-56 days). Deliberately Random.Shared instead of _random - otherwise
        // this purely cosmetic extra info would shift the deterministic game RNG stream and
        // break seed-dependent tests (e.g. HeaderAndSetPieceTests).
        private static int RollInjuryDurationDays()
        {
            var rng = Random.Shared;
            double severityRoll = rng.NextDouble();
            if (severityRoll < 0.6)
                return rng.Next(3, 11);
            if (severityRoll < 0.9)
                return rng.Next(14, 29);
            return rng.Next(28, 57);
        }

        // A strong physiotherapist/medical staff member shortens the injury duration - same
        // tiered form as DevelopmentService.MentorBonus, just using FitnessTraining as the
        // "medical" skill (no dedicated field needed, see StaffGenerator).
        private static int ApplyMedicalStaffReduction(int days, Team team)
        {
            var staff = team.Employees
                .Where(e => e.EmployeeType is EmployeeType.Physiotherapist or EmployeeType.MedicalStaff)
                .OrderByDescending(e => e.FitnessTraining)
                .FirstOrDefault();
            if (staff is null)
                return days;

            double factor = staff.FitnessTraining >= 75 ? 0.75 : staff.FitnessTraining >= 60 ? 0.9 : 1.0;
            return Math.Max(1, (int)Math.Round(days * factor));
        }

        // Issues yellow or red for a foul. A second yellow card for the same player in this
        // match automatically becomes a second-yellow red, regardless of whether "forceRed"
        // (too hard a foul) is set.
        private void ApplyFoulCard(
            MatchResult result,
            IProgress<MatchEvent>? progress,
            int minute,
            Player fouler,
            bool isHome,
            MatchStats matchStats,
            bool forceRed,
            Func<Random, string> redTextFactory)
        {
            if (forceRed)
            {
                IssueRedCard(result, progress, minute, fouler, isHome, matchStats, redTextFactory(_random), isSecondYellow: false);
                return;
            }

            var foulerStats = GetOrCreateMatchStats(result, fouler);
            if (foulerStats.YellowCards > 0)
            {
                IssueRedCard(result, progress, minute, fouler, isHome, matchStats,
                    EventTextHelper.RedCardSecondYellowText(fouler, _random), isSecondYellow: true);
                return;
            }

            matchStats.YellowCards++;
            (isHome ? result.HomeYellowCards : result.AwayYellowCards).Add(fouler);
            foulerStats.YellowCards++;
            EmitEvent(result, progress, minute, GameEventType.YellowCard, isHome, fouler,
                EventTextHelper.YellowCardText(fouler, _random));
        }

        // Straight red = 3-match ban, second yellow ("Gelb-Rot") = 1-match ban - actually
        // applied post-match via MatchResult.ApplySuspensions once the caller knows the
        // competition (league/cup/friendly).
        private const int StraightRedBanMatches = 3;
        private const int SecondYellowBanMatches = 1;

        private static void IssueRedCard(
            MatchResult result,
            IProgress<MatchEvent>? progress,
            int minute,
            Player player,
            bool isHome,
            MatchStats matchStats,
            string description,
            bool isSecondYellow)
        {
            matchStats.RedCards++;
            (isHome ? result.HomeRedCards : result.AwayRedCards).Add(player);
            GetOrCreateMatchStats(result, player).RedCards++;
            result.SuspensionMatchesByPlayerId[player.Id] = isSecondYellow ? SecondYellowBanMatches : StraightRedBanMatches;
            player.Status = PlayerStatus.Suspended;
            EmitEvent(result, progress, minute, GameEventType.RedCard, isHome, player, description);
        }

        private void DecayFitness(Team team, int minute)
        {
            if (minute % 5 != 0)
                return;

            var tactic = team.Tactic;
            double intensity = (tactic.PressingIntensityFactor + tactic.CounterSpeedFactor) / 2.0;
            double baseDecay = intensity * FitnessCoachFactor(team);

            foreach (var player in TeamStrengthCalculator.GetLineup(team))
            {
                int decay = Math.Max(1, (int)Math.Round(baseDecay * StaminaFactor(player)));
                player.Fitness = Math.Max(20, player.Fitness - decay);
            }
        }

        // Higher BaseFitness (Grundfitness) means less fatigue per tick: 1.3x decay at the
        // lowest possible value (1), 0.7x at the highest (99), linear in between.
        private static double StaminaFactor(Player player)
        {
            double t = Math.Clamp(player.BaseFitness, 1, 99) / 99.0;
            return 1.3 - (0.6 * t);
        }

        // A strong fitness coach makes players tire more slowly during the match (on top of
        // each player's own BaseFitness/Grundfitness - see StaminaFactor).
        private static double FitnessCoachFactor(Team team)
        {
            var coach = team.Employees
                .Where(e => e.EmployeeType == EmployeeType.FitnessCoach)
                .OrderByDescending(e => e.FitnessTraining)
                .FirstOrDefault();
            if (coach is null)
                return 1.0;

            return coach.FitnessTraining >= 75 ? 0.75 : coach.FitnessTraining >= 60 ? 0.9 : 1.0;
        }

        private void RecordPossessionSample(TeamStrengthProfile home, TeamStrengthProfile away)
        {
            double homeShare = home.Midfield / Math.Max(0.001, home.Midfield + away.Midfield);
            _homePossessionSum += homeShare;
            _awayPossessionSum += 1 - homeShare;
            _homePassAccuracySum += Math.Clamp(50 + (home.Midfield * 0.4), 0, 100);
            _awayPassAccuracySum += Math.Clamp(50 + (away.Midfield * 0.4), 0, 100);
            _sampledMinutes++;
        }

        private void FinalizePossessionAndPassing(MatchResult result)
        {
            if (_sampledMinutes == 0)
                return;

            result.MatchStatsHome.Possession = (int)Math.Round(_homePossessionSum / _sampledMinutes * 100);
            result.MatchStatsAway.Possession = 100 - result.MatchStatsHome.Possession;
            result.MatchStatsHome.PassAccuracy = (int)Math.Round(_homePassAccuracySum / _sampledMinutes);
            result.MatchStatsAway.PassAccuracy = (int)Math.Round(_awayPassAccuracySum / _sampledMinutes);
        }

        private bool Roll(double probability) => _random.NextDouble() < Math.Clamp(probability, 0, 1);

        private Player? PickWeightedPlayer(List<Player> players, Func<Player, double> weightSelector)
        {
            if (players.Count == 0)
                return null;

            double total = players.Sum(weightSelector);
            if (total <= 0)
                return players[_random.Next(players.Count)];

            double roll = _random.NextDouble() * total;
            double cumulative = 0;
            foreach (var player in players)
            {
                cumulative += weightSelector(player);
                if (roll <= cumulative)
                    return player;
            }

            return players[^1];
        }

        private static PlayerStats GetOrCreateMatchStats(MatchResult result, Player? player)
        {
            if (player is null)
                return new PlayerStats();

            if (!result.PlayerMatchStats.TryGetValue(player.Id, out var stats))
            {
                stats = new PlayerStats { PlayerId = player.Id };
                result.PlayerMatchStats[player.Id] = stats;
            }

            return stats;
        }

        private static void EmitEvent(
            MatchResult result,
            IProgress<MatchEvent>? progress,
            int minute,
            GameEventType type,
            bool isHomeTeam,
            Player? player,
            string description)
        {
            var evt = new MatchEvent(minute, type, isHomeTeam, player, description);
            result.Events.Add(evt);
            progress?.Report(evt);
        }
    }
}
