using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    // Regression tests for the "WB toggle lost / players swapped between confirming the
    // lineup and kickoff" bug: the match-day self-heal must only backfill genuinely empty
    // slots, never rebuild AssignedPosition/WingBack roles for players who are already
    // correctly placed (that destructive rebuild was the actual root cause).
    public class FillMissingStartersTests
    {
        private static Team BuildFullSquad()
        {
            // Seed 4 (was 2 - PlayerGenerator now also rolls InMatchCharacter per player,
            // shifting the RNG stream): with the current squad composition/position weights,
            // this seed reliably starts a natural LeftDefender at the LeftDefender/LeftWingBack
            // slot (needed as the baseline several of these tests toggle/compare against).
            var players = PlayerGenerator.GenerateSquad(Nationality.Germany, 60, squadSize: 25, random: new Random(4));
            int id = 1;
            foreach (var p in players)
                p.Id = id++;

            var team = new Team { Statistics = new TeamStats() };
            team.Players.AddRange(players);
            LineupSelector.SelectLineup(team, FormationCatalog.F442);
            return team;
        }

        [Fact]
        public void FillMissingStarters_DoesNotTouchExistingStarters_WhenXIIsAlreadyComplete()
        {
            var team = BuildFullSquad();
            var leftDefender = team.Players.First(p =>
                p.Status == PlayerStatus.InStartingXI && p.Position == Position.LeftDefender);
            leftDefender.AssignedPosition = Position.LeftWingBack; // manuell auf WB umgestellt

            var starterIdsBefore = team.Players
                .Where(p => p.Status == PlayerStatus.InStartingXI)
                .Select(p => p.Id)
                .OrderBy(id => id)
                .ToList();

            LineupSelector.FillMissingStarters(team, FormationCatalog.F442);

            var starterIdsAfter = team.Players
                .Where(p => p.Status == PlayerStatus.InStartingXI)
                .Select(p => p.Id)
                .OrderBy(id => id)
                .ToList();

            Assert.Equal(starterIdsBefore, starterIdsAfter);
            Assert.Equal(Position.LeftWingBack, leftDefender.AssignedPosition);
        }

        [Fact]
        public void FillMissingStarters_DoesNotConfuseTwoManuallySwappedStarters_WhenAThirdSlotIsEmpty()
        {
            // Regression test: two starters swap slots via a plain drag&drop (each gets an explicit
            // AssignedPosition equal to the other's native Position). The old matching also accepted
            // a bare native-Position match as a first-tier hit, so BOTH players matched the same slot
            // (one via AssignedPosition, one via native Position) and the winner depended purely on
            // list order - corrupting the pairing. Matching must go by EffectivePosition only.
            var team = BuildFullSquad();
            var a = team.Players.First(p => p.Status == PlayerStatus.InStartingXI && p.Position == Position.LeftMidfielder);
            var b = team.Players.First(p => p.Status == PlayerStatus.InStartingXI && p.Position == Position.CentralMidfielder);
            a.AssignedPosition = Position.CentralMidfielder; // a now effectively plays b's native slot
            b.AssignedPosition = Position.LeftMidfielder;    // b now effectively plays a's native slot

            var forward = team.Players.First(p =>
                p.Status == PlayerStatus.InStartingXI && p.Position == Position.Forward);
            forward.Status = PlayerStatus.Injured; // unrelated gap elsewhere in the XI

            LineupSelector.FillMissingStarters(team, FormationCatalog.F442);

            Assert.Equal(Position.CentralMidfielder, a.AssignedPosition);
            Assert.Equal(Position.LeftMidfielder, b.AssignedPosition);
            Assert.Equal(PlayerStatus.InStartingXI, a.Status);
            Assert.Equal(PlayerStatus.InStartingXI, b.Status);
        }

        [Fact]
        public void SelectLineup_PicksWingBackVariant_WhenABenchPlayerIsABetterLeftWingBackThanTheStartingLeftDefender()
        {
            // The AI must be able to reach the same WingBack decision a human makes via the WB
            // toggle: if it has a genuinely better LAV/RAV player than a plain LV/RV, it should
            // start him there (AssignedPosition = LeftWingBack), not ignore the alternate role.
            var team = BuildFullSquad();
            var currentLeftDefender = team.Players.First(p =>
                p.Status == PlayerStatus.InStartingXI && p.Position == Position.LeftDefender);

            var benchWingBack = team.Players.First(p => p.Status != PlayerStatus.InStartingXI);
            benchWingBack.Position = Position.LeftWingBack;
            benchWingBack.SecondaryPositionsRaw = "[]";
            // Lineup scoring is attribute/position-weighted (PlayerRoleRating), not a flat Rating
            // comparison - so make him clearly the stronger LeftWingBack across every attribute
            // (he may have been a bench goalkeeper, whose outfield-only stats sit at 0), not just
            // a higher generic Rating.
            benchWingBack.OffensivePower = 95;
            benchWingBack.DefensivePower = 95;
            benchWingBack.GameIntelligence = 95;
            benchWingBack.PressingIntensity = 95;
            benchWingBack.CounterSpeed = 95;
            benchWingBack.PassingAccuracy = 95;
            benchWingBack.DuelHardness = 95;
            benchWingBack.DuelEfficiency = 95;
            benchWingBack.CrossingAccuracy = 95;
            benchWingBack.HeaderStrength = 95;
            benchWingBack.Jumping = 95;
            benchWingBack.Dribbling = 95;
            benchWingBack.LongShotAccuracy = 95;
            benchWingBack.PenaltyKick = 95;
            benchWingBack.FreeKick = 95;
            benchWingBack.Finishing = 95;
            benchWingBack.Positioning = 95;
            benchWingBack.Rating = currentLeftDefender.Rating + 20; // clearly the stronger player

            LineupSelector.SelectLineup(team, FormationCatalog.F442);

            Assert.Equal(PlayerStatus.InStartingXI, benchWingBack.Status);
            Assert.Equal(Position.LeftWingBack, benchWingBack.EffectivePosition);
        }

        [Fact]
        public void ReloadingTheLineup_KeepsAWingBackToggle_ForAPlainLeftDefenderWithNoWingBackAbility()
        {
            // Reproduces the reported user scenario directly: set a plain LV (no WB ability
            // at all) to WingBack duty (LAV), then simulate "reloading the Aufstellung screen" -
            // which re-derives the pitch from Player state via LineupSelector.MatchStartersToSlots
            // (the exact same call LineupViewModel.BuildInitialLineup makes) - and confirm the
            // toggle is still there afterwards, not silently reset back to AV/base.
            var team = BuildFullSquad();
            var formation = FormationCatalog.F442;
            int ldSlotIndexForLookup = formation.Slots.ToList().FindIndex(s => s.Position == Position.LeftDefender);
            var initialMatch = LineupSelector.MatchStartersToSlots(
                team.Players.Where(p => p.Status == PlayerStatus.InStartingXI).ToList(), formation);
            var leftDefender = team.Players.First(p => p.Id == initialMatch[ldSlotIndexForLookup]);
            leftDefender.SecondaryPositionsRaw = "[]"; // plain LV, no WingBack ability whatsoever

            // Simulate LineupViewModel.ToggleSlotRole + ApplyLineup: toggling WB sets AssignedPosition.
            leftDefender.AssignedPosition = Position.LeftWingBack;

            // Simulate reopening the Aufstellung screen (BuildInitialLineup rebuilds from scratch).
            var starters = team.Players.Where(p => p.Status == PlayerStatus.InStartingXI).ToList();
            var matched = LineupSelector.MatchStartersToSlots(starters, formation);

            int ldSlotIndex = formation.Slots.ToList().FindIndex(s => s.Position == Position.LeftDefender);
            Assert.Equal(leftDefender.Id, matched[ldSlotIndex]);
            Assert.Equal(Position.LeftWingBack, leftDefender.AssignedPosition);
        }

        [Fact]
        public void MatchStartersToSlots_ExplicitOverrideWinsItsSlot_EvenWhenAnotherStarterNaturallySharesTheSameEffectivePosition()
        {
            // Regression test: a starter whose native Position IS e.g. RightWingBack (a real,
            // generatable primary position - see SelectLineup_PicksWingBackVariant... above) can
            // occupy an unrelated slot (here: right midfield) with AssignedPosition == null, so
            // his EffectivePosition also equals RightWingBack. A single-pass "EffectivePosition ==
            // slot.Position || == slot.AlternateRole" match can't tell him apart from the RD
            // player who was explicitly toggled to WingBack duty - both satisfy the RD slot's
            // AlternateRole check - and whoever comes first in list order wins, silently stealing
            // the explicitly-toggled player's role. Explicit overrides must always win their slot.
            var team = BuildFullSquad();
            var formation = FormationCatalog.F442;
            int rdSlotIndexForLookup = formation.Slots.ToList().FindIndex(s => s.Position == Position.RightDefender);
            int rmSlotIndex = formation.Slots.ToList().FindIndex(s => s.Position == Position.RightMidfielder);
            var beforeMatch = LineupSelector.MatchStartersToSlots(
                team.Players.Where(p => p.Status == PlayerStatus.InStartingXI).ToList(), formation);

            var rightDefender = team.Players.First(p => p.Id == beforeMatch[rdSlotIndexForLookup]);
            rightDefender.AssignedPosition = Position.RightWingBack; // explicit WB toggle

            var rightMidfielder = team.Players.First(p => p.Id == beforeMatch[rmSlotIndex]);
            rightMidfielder.Position = Position.RightWingBack; // naturally shares the same EffectivePosition
            rightMidfielder.AssignedPosition = null;

            var starters = team.Players.Where(p => p.Status == PlayerStatus.InStartingXI).ToList();
            var matched = LineupSelector.MatchStartersToSlots(starters, formation);

            int rdSlotIndex = formation.Slots.ToList().FindIndex(s => s.Position == Position.RightDefender);
            Assert.Equal(rightDefender.Id, matched[rdSlotIndex]);
        }

        [Fact]
        public void ReloadingTheLineup_DoesNotSwapTwoNativeWingBacksOnOppositeFlanks()
        {
            // Regression test: two starters whose native Position IS the wide slot's
            // AlternateRole (a real LAV/RAV specialist, not a toggled LV/RV) sit exactly on
            // their own natural flank. ApplyLineup then leaves AssignedPosition == null for
            // both (EffectivePosition already equals their native Position, no override
            // needed) - so pass 1 (explicit AssignedPosition) finds nothing for either slot.
            // The old pass 2 only checked "p.Position == slot.Position", never
            // slot.AlternateRole, so it missed them too, and both fell through to pass 3's
            // side-blind fallback - which could hand the LEFT slot to the RIGHT wingback and
            // vice versa, silently swapping them on every reload.
            var team = TestHelpers.CreateTeam("WB Test", baseRating: 60);
            var formation = FormationCatalog.F442;

            var leftWingBack = team.Players.First(p => p.Position == Position.LeftDefender);
            leftWingBack.Position = Position.LeftWingBack;
            var rightWingBack = team.Players.First(p => p.Position == Position.RightDefender);
            rightWingBack.Position = Position.RightWingBack;

            // Order matters for the old pass 3 fallback (picks "any remaining starter" in list
            // order, side-blind) - a real squad's Players list has no relation to formation slot
            // order, so list the RAV before the LAV to reproduce that failure mode reliably.
            var starters = team.Players.Where(p => p.Status == PlayerStatus.InStartingXI)
                .OrderBy(p => p.Id == rightWingBack.Id ? 0 : p.Id == leftWingBack.Id ? 1 : 2)
                .ToList();
            var matched = LineupSelector.MatchStartersToSlots(starters, formation);

            int ldSlotIndex = formation.Slots.ToList().FindIndex(s => s.Position == Position.LeftDefender);
            int rdSlotIndex = formation.Slots.ToList().FindIndex(s => s.Position == Position.RightDefender);
            Assert.Equal(leftWingBack.Id, matched[ldSlotIndex]);
            Assert.Equal(rightWingBack.Id, matched[rdSlotIndex]);
        }

        [Fact]
        public void FillMissingStarters_OnlyPromotesReplacementForTheMissingSlot()
        {
            var team = BuildFullSquad();
            var untouchedStarterAssignments = team.Players
                .Where(p => p.Status == PlayerStatus.InStartingXI)
                .ToDictionary(p => p.Id, p => p.AssignedPosition);

            var forward = team.Players.First(p =>
                p.Status == PlayerStatus.InStartingXI && p.Position == Position.Forward);
            forward.Status = PlayerStatus.Injured; // simuliert eine Verletzung nach dem Bestätigen

            LineupSelector.FillMissingStarters(team, FormationCatalog.F442);

            Assert.Equal(11, team.Players.Count(p => p.Status == PlayerStatus.InStartingXI));
            foreach (var (id, assignedPosition) in untouchedStarterAssignments)
            {
                if (id == forward.Id)
                    continue;
                var player = team.Players.First(p => p.Id == id);
                Assert.Equal(PlayerStatus.InStartingXI, player.Status);
                Assert.Equal(assignedPosition, player.AssignedPosition);
            }
        }
    }
}
