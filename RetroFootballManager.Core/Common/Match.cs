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
        // A shot that misses the target often still gets deflected/blocked behind for a
        // corner instead of a clean goal kick - real matches get most of their corners this
        // way, not from keeper-save rebounds (SaveReboundCornerChance above), which is why
        // that alone left corners near-zero.
        private const double MissedShotCornerChance = 0.35;
        private const double PenaltyChance = 0.012;
        private const double PenaltyConversionBase = 0.78;
        private const double PenaltySavedGivenMissChance = 0.65;
        private const double PenaltyRedCardChance = 0.06;

        // "Average" player attribute value for this game's rating scale - the reference every
        // league-average-relative check below (offside, free-kick taking) is normalized
        // against, instead of a live per-tier query Match has no access to (it only ever sees
        // the two teams actually playing). Matches the typical mid-table generation baseline.
        private const double LeagueAverageReference = 60.0;

        // Share of shots that are header attempts vs. long-range attempts (rest are normal
        // open-play shots) - see ResolveShotType/HeaderPower below.
        private const double HeaderAttemptShare = 0.22;
        private const double LongShotAttemptShare = 0.16;
        // Direct free-kick chance per dangerous attack (a foul just outside the box).
        private const double FreeKickChance = 0.006;
        private const double FreeKickConversionBase = 0.10;
        // Minimum minutes played for a clean sheet to count (same reference as
        // MatchRatingCalculator's clean-sheet rating bonus) - a keeper who only came on for
        // the last few minutes of a shutout shouldn't be credited a clean sheet.
        private const int CleanSheetMinMinutes = 60;

        private readonly Team _homeTeam;
        private readonly Team _awayTeam;
        private readonly Random _random;

        private double _homePossessionSum;
        private double _awayPossessionSum;
        private int _sampledMinutes;

        // Per-player InMatchMoral baseline (PlayerId -> value at kickoff, after the
        // SlowStarter deduction) - ApplyInMatchMoraleDrift slowly pulls InMatchMoral back
        // toward this, not toward a shared constant.
        private readonly Dictionary<int, int> _moraleBaseline = [];
        private const int MinutesBetweenMoraleDrift = 5;
        private const double MoraleDriftFraction = 0.1;
        private const int LowMoraleThreshold = 40;

        private MatchResult _result = new();
        private IProgress<MatchEvent>? _progress;
        private int _minute;
        private int _firstHalfEnd;
        private int _secondHalfEnd;
        private int _homeSubsUsed;
        private int _awaySubsUsed;
        private bool _homeEmotionalTeamTalkUsed;
        private bool _awayEmotionalTeamTalkUsed;

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

        // "Emotional aufbauen" team talk is usable once per match per side (see TeamTalkService).
        public bool HasUsedEmotionalTeamTalk(bool isHome) => isHome ? _homeEmotionalTeamTalkUsed : _awayEmotionalTeamTalkUsed;

        public void MarkEmotionalTeamTalkUsed(bool isHome)
        {
            if (isHome) _homeEmotionalTeamTalkUsed = true;
            else _awayEmotionalTeamTalkUsed = true;
        }

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

            SeedInMatchMoral(_homeTeam);
            SeedInMatchMoral(_awayTeam);

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
            ApplyInMatchMoraleDrift(_minute);
            RunCoaches();

            if (Phase == MatchPhase.FirstHalf && _minute >= _firstHalfEnd)
            {
                EmitEvent(_result, _progress, _firstHalfEnd, GameEventType.HalfTime, true, null, "Halbzeit");
                ApplyHalfTimeCharacterEffects();
                Phase = MatchPhase.HalfTime;
            }
            else if (Phase == MatchPhase.SecondHalf && _minute >= _secondHalfEnd)
            {
                EmitEvent(_result, _progress, _secondHalfEnd, GameEventType.FullTime, true, null,
                    $"Abpfiff: {_homeTeam.Name} {_result.HomeGoals}:{_result.AwayGoals} {_awayTeam.Name}");
                FinalizeRatings(_result);
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
            {
                _result.MinutesPlayed[p.Id] = _result.MinutesPlayed.GetValueOrDefault(p.Id) + 1;
                GetOrCreateMatchStats(_result, p);
            }
            foreach (var p in TeamStrengthCalculator.GetLineup(_awayTeam))
            {
                _result.MinutesPlayed[p.Id] = _result.MinutesPlayed.GetValueOrDefault(p.Id) + 1;
                GetOrCreateMatchStats(_result, p);
            }

            if (IsOffensiveOrientation(_homeTeam.TacticalOrientation)) _result.HomeOffensiveOrientationMinutes++;
            else if (IsDefensiveOrientation(_homeTeam.TacticalOrientation)) _result.HomeDefensiveOrientationMinutes++;
            if (IsOffensiveOrientation(_awayTeam.TacticalOrientation)) _result.AwayOffensiveOrientationMinutes++;
            else if (IsDefensiveOrientation(_awayTeam.TacticalOrientation)) _result.AwayDefensiveOrientationMinutes++;
        }

        private static bool IsOffensiveOrientation(TacticalOrientation orientation) =>
            orientation is TacticalOrientation.Offensive or TacticalOrientation.VeryOffensive;

        private static bool IsDefensiveOrientation(TacticalOrientation orientation) =>
            orientation is TacticalOrientation.Defensive or TacticalOrientation.VeryDefensive;

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
            PlayingStyle.TikiTaka => "Ballbesitz",
            PlayingStyle.Pressing => "Pressing",
            PlayingStyle.WingPlay => "Flügelspiel",
            PlayingStyle.CrossesToStriker => "Flanken auf Stürmer",
            _ => style.ToString(),
        };

        private void SimulateMinute(MatchResult result, IProgress<MatchEvent>? progress, int minute)
        {
            var homeProfile = TeamStrengthCalculator.Calculate(_homeTeam, isHome: true);
            var awayProfile = TeamStrengthCalculator.Calculate(_awayTeam, isHome: false);

            RecordPossessionSample(result, homeProfile, awayProfile);

            var homeLineup = TeamStrengthCalculator.GetLineup(_homeTeam);
            var awayLineup = TeamStrengthCalculator.GetLineup(_awayTeam);

            ProcessAttack(result, progress, minute, _homeTeam, _awayTeam, homeProfile, awayProfile,
                isHomeAttacking: true, result.MatchStatsHome, result.MatchStatsAway);
            ProcessAttack(result, progress, minute, _awayTeam, _homeTeam, awayProfile, homeProfile,
                isHomeAttacking: false, result.MatchStatsAway, result.MatchStatsHome);

            ProcessPassingAndCrossing(result, homeLineup, _homeTeam, awayLineup, _awayTeam);
            ProcessPassingAndCrossing(result, awayLineup, _awayTeam, homeLineup, _homeTeam);

            ProcessGroundDuels(result, homeLineup, _homeTeam, awayLineup, _awayTeam);
            ProcessGroundDuels(result, awayLineup, _awayTeam, homeLineup, _homeTeam);

            ProcessDisciplineAndInjury(result, progress, minute, _homeTeam, homeProfile, isHome: true, result.MatchStatsHome, homeLineup);
            ProcessDisciplineAndInjury(result, progress, minute, _awayTeam, awayProfile, isHome: false, result.MatchStatsAway, awayLineup);

            DecayFitness(_homeTeam, minute);
            DecayFitness(_awayTeam, minute);
        }

        // Per-minute, per-team passing simulation: 3-6 pass attempts weighted by
        // PassingAccuracy, success rolled against the passer's own accuracy. Crossing is
        // handled separately (ProcessCrossAttempt) since it's restricted to wide positions
        // and directly triggers a header duel.
        private void ProcessPassingAndCrossing(
            MatchResult result, List<Player> lineup, Team owningTeam, List<Player> defendingLineup, Team defendingTeam)
        {
            if (lineup.Count == 0)
                return;

            // In-Game-Coaching sharpens the manager's own side's effective passing accuracy -
            // purely a magnitude scaler on top of the existing PassingAccuracy-weighted roll.
            double passingFactor = ManagerEffects.InGameCoachingFactor(owningTeam.ManagerProfile);

            int passAttempts = _random.Next(3, 7);
            for (int i = 0; i < passAttempts; i++)
            {
                var passer = PickWeightedPlayer(lineup, p => p.PassingAccuracy);
                if (passer is null)
                    continue;

                var passerStats = GetOrCreateMatchStats(result, passer);
                passerStats.Passes++;
                double effectiveAccuracy = Math.Clamp(passer.PassingAccuracy * passingFactor, 0, 100);
                if (Roll(effectiveAccuracy / 100.0))
                    passerStats.SuccessfulPasses++;
            }

            ProcessCrossAttempt(result, lineup, owningTeam, defendingLineup, defendingTeam);
        }

        private static readonly Position[] WidePositions =
        {
            Position.LeftDefender, Position.RightDefender, Position.LeftWingBack, Position.RightWingBack,
            Position.LeftMidfielder, Position.RightMidfielder, Position.LeftOffenseMidfielder, Position.RightOffenseMidfielder,
        };

        private void ProcessCrossAttempt(
            MatchResult result, List<Player> lineup, Team owningTeam, List<Player> defendingLineup, Team defendingTeam)
        {
            if (!Roll(0.20))
                return;

            var wide = lineup.Where(p => WidePositions.Contains(p.EffectivePosition)).ToList();
            var crosser = PickWeightedPlayer(wide, p => p.CrossingAccuracy * p.CrossingAccuracy);
            if (crosser is null)
                return;

            var crosserStats = GetOrCreateMatchStats(result, crosser);
            crosserStats.Crosses++;
            if (Roll(crosser.CrossingAccuracy / 100.0))
                crosserStats.SuccessfulCrosses++;

            // Header duels arise from crosses regardless of whether the cross itself is
            // deemed "successful" - reuses the same HeaderPower/BestHeaderDefender logic as
            // the header-goal chance in ProcessAttack, purely additive for statistics here.
            var attacker = PickWeightedPlayer(lineup, HeaderPower);
            var defender = BestHeaderDefender(defendingLineup);
            if (attacker is null || defender is null)
                return;

            double attackerPower = HeaderPower(attacker);
            // A defender with poor Positioning (reading crosses/runs) loses more aerial duels
            // than their raw HeaderPower alone would suggest; a defensively-minded manager's
            // In-Game-Coaching sharpens that reading further.
            double defenderPower = HeaderPower(defender)
                * (IsDefensivePosition(defender.EffectivePosition)
                    ? PositioningFactor(defender) * ManagerEffects.InGameCoachingFactor(defendingTeam.ManagerProfile)
                    : 1.0);
            double attackerWinProb = attackerPower / Math.Max(0.001, attackerPower + defenderPower);

            var attackerStats = GetOrCreateMatchStats(result, attacker);
            var defenderStats = GetOrCreateMatchStats(result, defender);
            attackerStats.HeaderDuels++;
            defenderStats.HeaderDuels++;
            if (Roll(attackerWinProb))
                attackerStats.HeaderDuelsWon++;
            else
                defenderStats.HeaderDuelsWon++;
        }

        // 2-4 ground duels per minute and attacking direction: ball carrier (weighted by
        // Dribbling/OffensivePower) against a defender (weighted by DuelHardness/
        // DefensivePower, goalkeeper excluded). Winner decided by DuelEfficiency - the same
        // duel from both perspectives (defender: Tackles/TacklesWon, attacker: Dribbles/
        // SuccessfulDribbles).
        private void ProcessGroundDuels(
            MatchResult result, List<Player> attackingLineup, Team attackingTeam, List<Player> defendingLineup, Team defendingTeam)
        {
            var defenders = defendingLineup.Where(p => p.EffectivePosition != Position.Goalkeeper).ToList();
            if (attackingLineup.Count == 0 || defenders.Count == 0)
                return;

            double attackFactor = ManagerEffects.InGameCoachingFactor(attackingTeam.ManagerProfile);
            double defenseFactor = ManagerEffects.InGameCoachingFactor(defendingTeam.ManagerProfile);

            int duelCount = _random.Next(2, 5);
            for (int i = 0; i < duelCount; i++)
            {
                var ballCarrier = PickWeightedPlayer(attackingLineup, p => p.Dribbling * (1 + (p.OffensivePower / 100.0)));
                var defender = PickWeightedPlayer(defenders, p => p.DuelHardness * (1 + (p.DefensivePower / 100.0)));
                if (ballCarrier is null || defender is null)
                    continue;

                var carrierStats = GetOrCreateMatchStats(result, ballCarrier);
                var defenderStats = GetOrCreateMatchStats(result, defender);
                carrierStats.Dribbles++;
                defenderStats.Tackles++;

                // A midfielder with poor Positioning arrives late to challenges, losing more
                // ground duels than their raw DuelEfficiency alone would suggest. Each side's
                // own In-Game-Coaching sharpens its own player's effective DuelEfficiency.
                double defenderDuelEfficiency = defender.DuelEfficiency
                    * (IsMidfieldPosition(defender.EffectivePosition) ? PositioningFactor(defender) : 1.0)
                    * defenseFactor;
                double attackerDuelEfficiency = ballCarrier.DuelEfficiency * attackFactor;
                double defenderWinProb = defenderDuelEfficiency / Math.Max(0.001, defenderDuelEfficiency + attackerDuelEfficiency);
                if (Roll(defenderWinProb))
                    defenderStats.TacklesWon++;
                else
                    carrierStats.SuccessfulDribbles++;
            }
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

            // Offside: only advanced attackers can be caught out - chance scales inversely
            // with Positioning (reading the run of play) and with the team's own passing
            // quality (a side playing sharper, more line-breaking final balls attempts more
            // ambitious through-balls that flirt with the offside line - see OffsideChance).
            // Header attempts are excluded: offside there is a crowding/near-post read, not the
            // through-ball timing this models, and a dominant aerial team getting picked as the
            // header-shooter far more often than an average team would otherwise rack up far
            // more offside chances too - enough, at scale, to actually outweigh their real
            // scoring advantage and net them FEWER goals than a weak aerial team.
            if (!isHeader && shooter is not null && IsAdvancedPosition(shooter.EffectivePosition)
                && Roll(OffsideChance(shooter, lineup)))
            {
                attackingStats.Offsides++;
                GetOrCreateMatchStats(result, shooter).Offsides++;
                EmitEvent(result, progress, minute, GameEventType.Offside, isHomeAttacking, shooter,
                    EventTextHelper.OffsideText(shooter, _random));
                return;
            }

            attackingStats.Shots++;
            GetOrCreateMatchStats(result, shooter).Shots++;
            EmitEvent(result, progress, minute, GameEventType.Shot, isHomeAttacking, shooter,
                shooter is not null ? EventTextHelper.ShotText(shooter, _random) : "Der Ball wird abgeschlossen.");

            if (!Roll(OnTargetBase * (attackShare / 0.5)))
            {
                // A blocked/deflected off-target effort often still goes out for a corner
                // instead of a clean goal kick - the other, rarer corner source is a keeper
                // save that rebounds (SaveReboundCornerChance below).
                if (Roll(MissedShotCornerChance))
                {
                    attackingStats.Corners++;
                    EmitEvent(result, progress, minute, GameEventType.Corner, isHomeAttacking, null,
                        EventTextHelper.CornerText(attacking, _random));
                }
                return;
            }

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
                    allowAssist: true, preferredAssist: preferredAssist, concedingGoalkeeper: goalkeeper);
            }
            else
            {
                EmitEvent(result, progress, minute, GameEventType.Save, !isHomeAttacking, goalkeeper,
                    EventTextHelper.SaveText(defending, _random));
                if (goalkeeper is not null)
                    GetOrCreateMatchStats(result, goalkeeper).Saves++;

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

        // Raised from 0.05 - at the old rate, combined with the narrow set of checks that
        // roll it (only advanced-position shot attempts), offsides landed at ~0/match, well
        // below the realistic ~1-3/team/game this is calibrated against.
        private const double OffsideBaseChance = 0.07;

        private static readonly Position[] AdvancedPositions =
        {
            Position.Forward, Position.CentralOffenseMidfielder,
            Position.LeftOffenseMidfielder, Position.RightOffenseMidfielder,
        };

        private static readonly Position[] DefensivePositions =
        {
            Position.CentralDefender, Position.LeftDefender, Position.RightDefender,
            Position.LeftWingBack, Position.RightWingBack,
        };

        private static readonly Position[] MidfieldPositions =
        {
            Position.DefensiveMidfielder, Position.CentralMidfielder,
            Position.LeftMidfielder, Position.RightMidfielder,
        };

        private static bool IsAdvancedPosition(Position position) => AdvancedPositions.Contains(position);
        private static bool IsDefensivePosition(Position position) => DefensivePositions.Contains(position);
        private static bool IsMidfieldPosition(Position position) => MidfieldPositions.Contains(position);

        // Ranges 0.7 (Positioning 1, worst reading of the game) to 1.0 (Positioning 99) -
        // a multiplier applied to power/win-probability in duels sensitive to positioning.
        private static double PositioningFactor(Player p) =>
            0.7 + (Math.Clamp(p.Positioning, 1, 99) / 99.0 * 0.3);

        // Worse Positioning roughly doubles the offside chance versus a player with elite
        // positioning (0.5x-1.5x around the base rate), blended with the attacking team's own
        // passing quality relative to LeagueAverageReference - a side playing sharper,
        // line-breaking final balls attempts more ambitious through-balls that flirt with the
        // offside line, a side with modest passing plays it safer (and gets flagged less).
        private static double OffsideChance(Player shooter, List<Player> attackingLineup)
        {
            // Tighter band than the positioning factor below (0.85x-1.15x, not 0.7x-1.4x) - the
            // two stack multiplicatively, and this one is a secondary influence (team-wide
            // average) layered on the shooter's own, more decisive Positioning read.
            double passingFactor = Math.Clamp(
                attackingLineup.Average(p => p.PassingAccuracy) / LeagueAverageReference, 0.85, 1.15);
            double positioningFactor = 1.5 - (Math.Clamp(shooter.Positioning, 1, 99) / 99.0);
            return OffsideBaseChance * passingFactor * positioningFactor;
        }

        private void RegisterGoal(
            MatchResult result,
            IProgress<MatchEvent>? progress,
            int minute,
            Team attacking,
            bool isHomeAttacking,
            Player? shooter,
            MatchStats attackingStats,
            bool allowAssist,
            Player? preferredAssist = null,
            Player? concedingGoalkeeper = null)
        {
            attackingStats.Goals++;
            if (concedingGoalkeeper is not null)
                GetOrCreateMatchStats(result, concedingGoalkeeper).GoalsConceded++;

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

            ApplyGoalMoraleReactions(isHomeAttacking);

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
                   * TacklingIntensityEffects.GetFoulCardRiskMultiplier(p, defending)
                   * LowMoraleFoulRiskMultiplier(p));

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
                RegisterGoal(result, progress, minute, attacking, isHomeAttacking, taker, attackingStats,
                    allowAssist: false, concedingGoalkeeper: goalkeeper);
            }
            else if (taker is not null && Roll(PenaltySavedGivenMissChance))
            {
                attackingStats.ShotsOnTarget++;
                GetOrCreateMatchStats(result, taker).ShotsOnTarget++;
                EmitEvent(result, progress, minute, GameEventType.Save, !isHomeAttacking, goalkeeper,
                    EventTextHelper.PenaltySavedText(defending, taker, _random));
                if (goalkeeper is not null)
                    GetOrCreateMatchStats(result, goalkeeper).Saves++;
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

            EmitEvent(result, progress, minute, GameEventType.FreeKick, isHomeAttacking, taker,
                EventTextHelper.FreeKickAwardedText(attacking, taker, _random));

            attackingStats.FreeKicks++;
            attackingStats.Shots++;
            GetOrCreateMatchStats(result, taker).Shots++;
            EmitEvent(result, progress, minute, GameEventType.Shot, isHomeAttacking, taker,
                EventTextHelper.ShotText(taker, _random));

            var goalkeeper = TeamStrengthCalculator.GetGoalkeeper(defending);
            double keeperAbility = goalkeeper is not null
                ? (goalkeeper.GkReflexes * 0.5) + (goalkeeper.GkOneOnOne * 0.3) + (goalkeeper.GkHandling * 0.2)
                : 50;
            double duelRatio = taker.FreeKick / Math.Max(0.001, taker.FreeKick + keeperAbility);
            // The base rate itself scales with the taker's quality relative to
            // LeagueAverageReference, on top of the direct taker-vs-keeper duel above - a
            // genuinely weak taker underperforms even against an average keeper, not just
            // relative to that one keeper.
            double takerQualityFactor = Math.Clamp(taker.FreeKick / LeagueAverageReference, 0.6, 1.6);
            double conversionProb = Math.Clamp(
                (FreeKickConversionBase * takerQualityFactor) + ((duelRatio - 0.5) * 0.5), 0.02, 0.45);

            if (Roll(conversionProb))
            {
                attackingStats.ShotsOnTarget++;
                GetOrCreateMatchStats(result, taker).ShotsOnTarget++;
                EmitEvent(result, progress, minute, GameEventType.ShotOnTarget, isHomeAttacking, taker,
                    EventTextHelper.ShotOnTargetText(taker, _random));
                RegisterGoal(result, progress, minute, attacking, isHomeAttacking, taker, attackingStats,
                    allowAssist: false, concedingGoalkeeper: goalkeeper);
            }
            else
            {
                EmitEvent(result, progress, minute, GameEventType.Save, !isHomeAttacking, goalkeeper,
                    EventTextHelper.SaveText(defending, _random));
                if (goalkeeper is not null)
                    GetOrCreateMatchStats(result, goalkeeper).Saves++;
            }
        }

        private void ProcessDisciplineAndInjury(
            MatchResult result,
            IProgress<MatchEvent>? progress,
            int minute,
            Team team,
            TeamStrengthProfile profile,
            bool isHome,
            MatchStats matchStats,
            List<Player> lineup)
        {
            if (lineup.Count == 0)
                return;

            if (Roll(FoulBase * (1 + profile.DisciplineRisk)))
            {
                var fouler = PickWeightedPlayer(lineup,
                    p => p.DuelHardness * PersonalityEffects.Get(p.Personality).FoulChance
                       * TacklingIntensityEffects.GetFoulCardRiskMultiplier(p, team)
                       * LowMoraleFoulRiskMultiplier(p));

                matchStats.Fouls++;
                if (fouler is not null)
                    GetOrCreateMatchStats(result, fouler).Fouls++;

                // Every foul awards the fouled side a free kick - this is what the
                // "Freistöße" stat should actually track (it used to only count the separate,
                // deliberately rare ResolveFreeKick "direct shot at goal" special case, which
                // left it at 0 in most matches despite a realistic foul count). No extra ticker
                // line - the Foul event below already narrates it.
                (isHome ? result.MatchStatsAway : result.MatchStatsHome).FreeKicks++;

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

        // The whole Physiotherapist+MedicalStaff pool shortens injury duration (stacks across
        // multiple hires, unlike the best-of-type pattern elsewhere) - tiered form as
        // DevelopmentService.MentorBonus, using FitnessTraining as the "medical" skill (no
        // dedicated field needed, see StaffGenerator). Scaled by an overload factor: plenty of
        // staff relative to how many players are currently injured keeps the full reduction,
        // too few for too many injuries stretches them thin and can even lengthen recovery.
        // Public (like PenaltyConversionProbability above) - pure and deterministic given its
        // inputs, so it's directly unit-testable without needing to force a rare in-match
        // injury roll (InjuryBase is tiny) across many simulated matches.
        public static int ApplyMedicalStaffReduction(int days, Team team)
        {
            var staff = team.Employees
                .Where(e => e.EmployeeType is EmployeeType.Physiotherapist or EmployeeType.MedicalStaff)
                .ToList();
            if (staff.Count == 0)
                return days;

            int currentlyInjured = Math.Max(1, team.Players.Count(p => p.Status == PlayerStatus.Injured));
            double avgFitnessTraining = staff.Average(e => e.FitnessTraining);
            double staffPerInjured = staff.Count / (double)currentlyInjured;
            double overloadFactor = Math.Clamp(1.5 - (staffPerInjured * 0.5), 0.7, 1.5);

            double baseFactor = avgFitnessTraining >= 75 ? 0.75 : avgFitnessTraining >= 60 ? 0.9 : 1.0;
            double factor = Math.Clamp(baseFactor * overloadFactor, 0.4, 1.8);
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

        // Seeds InMatchMoral from the persistent Moral at kickoff (for the whole squad, not
        // just the starting XI, so a substitute coming on later also has a proper value
        // instead of a stale one from a previous match), applies the one-time SlowStarter
        // deduction, and records the post-deduction value as this match's drift baseline.
        private void SeedInMatchMoral(Team team)
        {
            foreach (var player in team.Players)
            {
                int seeded = player.Moral + (int)InMatchCharacterEffects.Get(player.InMatchCharacter).SlowStartPenalty;
                player.InMatchMoral = Math.Clamp(seeded, 0, 100);
                _moraleBaseline[player.Id] = player.InMatchMoral;
            }
        }

        // Every 5 minutes, InMatchMoral slowly regresses toward each player's own baseline
        // (see SeedInMatchMoral) instead of staying wherever a goal/team-talk left it -
        // MoraleVolatility scales how fast a character drifts back (IceCold barely moved away
        // from baseline in the first place, so this mostly matters for volatile characters).
        private void ApplyInMatchMoraleDrift(int minute)
        {
            if (minute % MinutesBetweenMoraleDrift != 0)
                return;

            foreach (var player in OnPitch(isHome: true).Concat(OnPitch(isHome: false)))
            {
                if (!_moraleBaseline.TryGetValue(player.Id, out int baseline))
                    continue;

                double volatility = InMatchCharacterEffects.Get(player.InMatchCharacter).MoraleVolatility;
                int drift = (int)Math.Round((baseline - player.InMatchMoral) * MoraleDriftFraction * volatility);
                player.InMatchMoral = Math.Clamp(player.InMatchMoral + drift, 0, 100);
            }
        }

        // At half-time, players react to the scoreline from their side's perspective - behind
        // dampened by BehindResilience, comfortably ahead (2+ goals) amplified by
        // LeadComplacency (see InMatchCharacterEffects; Complacent/LazyWhenLeading feel this
        // most). Runs once, at the FirstHalf -> HalfTime transition, before the team-talk
        // dialog/AI choice.
        private const int HalfTimeScorelineDelta = 8;
        private const int BigLeadGoalDiff = 2;

        private void ApplyHalfTimeCharacterEffects()
        {
            int goalDiff = _result.HomeGoals - _result.AwayGoals;
            ApplyHalfTimeSideEffect(isHome: true, goalDiff);
            ApplyHalfTimeSideEffect(isHome: false, goalDiff: -goalDiff);
        }

        private void ApplyHalfTimeSideEffect(bool isHome, int goalDiff)
        {
            foreach (var player in OnPitch(isHome))
            {
                var mod = InMatchCharacterEffects.Get(player.InMatchCharacter);
                int delta = 0;
                if (goalDiff < 0)
                    delta = -(int)Math.Round(HalfTimeScorelineDelta / mod.BehindResilience);
                else if (goalDiff >= BigLeadGoalDiff)
                    delta = -(int)Math.Round(HalfTimeScorelineDelta * 0.5 * mod.LeadComplacency);

                if (delta != 0)
                    player.InMatchMoral = Math.Clamp(player.InMatchMoral + delta, 0, 100);
            }
        }

        // Extra foul-risk weighting once a player is rattled (InMatchMoral < 40) - scaled by
        // his character's LowMoraleFoulRisk (Hothead/RiskTaker feel this most, others are
        // unaffected via the neutral 1.0 default).
        private static double LowMoraleFoulRiskMultiplier(Player p) =>
            p.InMatchMoral < LowMoraleThreshold ? InMatchCharacterEffects.Get(p.InMatchCharacter).LowMoraleFoulRisk : 1.0;

        // A goal shifts morale on both sides: up for the scoring side (more for
        // MomentumHunter/MomentumSensitive), down for the conceding side (more for
        // NervousUnderPressure/FragileConfidence/MomentumSensitive, less for Fighter).
        private const int GoalMoraleDelta = 6;

        private void ApplyGoalMoraleReactions(bool isHomeScoring)
        {
            foreach (var player in OnPitch(isHomeScoring))
            {
                double factor = InMatchCharacterEffects.Get(player.InMatchCharacter).GoalReactionFactor;
                player.InMatchMoral = Math.Clamp(player.InMatchMoral + (int)Math.Round(GoalMoraleDelta * factor), 0, 100);
            }

            foreach (var player in OnPitch(!isHomeScoring))
            {
                double factor = InMatchCharacterEffects.Get(player.InMatchCharacter).ConcededReactionFactor;
                player.InMatchMoral = Math.Clamp(player.InMatchMoral - (int)Math.Round(GoalMoraleDelta * factor), 0, 100);
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

        // Possession/pass stats are recomputed every minute (not just once at full-time) so
        // a Statistik dialog opened mid-match already shows meaningful, current numbers.
        private void RecordPossessionSample(MatchResult result, TeamStrengthProfile home, TeamStrengthProfile away)
        {
            double homeQuality = PossessionQuality(_homeTeam, home, away, _awayTeam);
            double awayQuality = PossessionQuality(_awayTeam, away, home, _homeTeam);
            double homeShare = homeQuality / Math.Max(0.001, homeQuality + awayQuality);

            _homePossessionSum += homeShare;
            _awayPossessionSum += 1 - homeShare;
            _sampledMinutes++;

            result.MatchStatsHome.Possession = (int)Math.Round(_homePossessionSum / _sampledMinutes * 100);
            result.MatchStatsAway.Possession = 100 - result.MatchStatsHome.Possession;

            UpdateLivePassStats(result);
        }

        // How well a team retains the ball this minute: own Midfield (already tactic-/style-
        // aware - TikiTaka boosts PassingAccuracy/GameIntelligence weighting, see Tactic.cs)
        // blended with the lineup's average Positioning (reading the game under pressure),
        // dampened by how hard the opponent disrupts play - both their Pressing tactic
        // (profile value) and their lineup's average DuelHardness (Zweikampfhärte, physical
        // tackling regardless of tactic) make it harder to keep the ball; a defensive,
        // physically softer opponent makes it easier.
        private static double PossessionQuality(
            Team team, TeamStrengthProfile own, TeamStrengthProfile opponentProfile, Team opponentTeam)
        {
            var lineup = TeamStrengthCalculator.GetLineup(team);
            double positioningAvg = lineup.Count == 0 ? 50 : lineup.Average(p => p.Positioning);
            double buildUpQuality = (own.Midfield * 0.7) + (positioningAvg * 0.3);

            var opponentLineup = TeamStrengthCalculator.GetLineup(opponentTeam);
            double opponentDuelHardnessAvg = opponentLineup.Count == 0 ? 50 : opponentLineup.Average(p => p.DuelHardness);
            double disruption = (opponentProfile.Pressing / 100.0) + (opponentDuelHardnessAvg / 200.0);

            return buildUpQuality / (1.0 + disruption);
        }

        // Aggregates each team's Passes/SuccessfulPasses/PassAccuracy from the real per-player
        // pass events recorded so far (ProcessPassingAndCrossing) - called every minute so it
        // stays current for a live Statistik dialog, not just at full-time.
        private void UpdateLivePassStats(MatchResult result)
        {
            var homeIds = _homeTeam.Players.Select(p => p.Id).ToHashSet();
            int homePasses = 0, homeSuccessful = 0, awayPasses = 0, awaySuccessful = 0;

            foreach (var stats in result.PlayerMatchStats.Values)
            {
                if (homeIds.Contains(stats.PlayerId))
                {
                    homePasses += stats.Passes;
                    homeSuccessful += stats.SuccessfulPasses;
                }
                else
                {
                    awayPasses += stats.Passes;
                    awaySuccessful += stats.SuccessfulPasses;
                }
            }

            result.MatchStatsHome.Passes = homePasses;
            result.MatchStatsHome.SuccessfulPasses = homeSuccessful;
            result.MatchStatsHome.PassAccuracy = homePasses > 0 ? (int)Math.Round((double)homeSuccessful / homePasses * 100) : 0;

            result.MatchStatsAway.Passes = awayPasses;
            result.MatchStatsAway.SuccessfulPasses = awaySuccessful;
            result.MatchStatsAway.PassAccuracy = awayPasses > 0 ? (int)Math.Round((double)awaySuccessful / awayPasses * 100) : 0;
        }

        // Computes the final match rating for every player with recorded stats, using their
        // final MinutesPlayed - called exactly once per match, at full-time.
        private void FinalizeRatings(MatchResult result)
        {
            foreach (var (playerId, stats) in result.PlayerMatchStats)
            {
                var player = _homeTeam.Players.FirstOrDefault(p => p.Id == playerId)
                    ?? _awayTeam.Players.FirstOrDefault(p => p.Id == playerId);
                if (player is null)
                    continue;

                int minutesPlayed = result.MinutesPlayed.GetValueOrDefault(playerId);
                stats.Rating = MatchRatingCalculator.Calculate(stats, player.EffectivePosition, minutesPlayed);

                if (player.EffectivePosition == Position.Goalkeeper
                    && stats.GoalsConceded == 0
                    && minutesPlayed >= CleanSheetMinMinutes)
                {
                    stats.CleanSheets = 1;
                }
            }
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
