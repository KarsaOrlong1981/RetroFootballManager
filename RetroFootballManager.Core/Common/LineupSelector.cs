using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Picks a position-correct starting XI for a formation and fills the bench.
    // A real goalkeeper is always chosen in goal; outfield slots are filled greedily by
    // the best position fit (primary position, then secondary positions, then a penalty
    // for playing out of position). Used by the COM before each match and by the AI coach
    // to find sensible substitutes. The human uses the same logic as an auto-pick default.
    public static class LineupSelector
    {
        // Both the initial pick's bench size (SelectLineup) and the minimum bench strength
        // RefillBench/LineupViewModel top back up to whenever a departure or an old
        // under-filled save leaves it short - the two are deliberately the same number so a
        // freshly repaired bench always looks exactly like a freshly picked one.
        private const int BenchSize = 9;

        // Rebuilds the whole matchday squad: sets 11 starters (Status/AssignedPosition),
        // up to 9 bench players and leaves the rest as reserves. Injured/suspended players
        // are never selected.
        public static List<Player> SelectLineup(Team team, Formation? formation = null)
        {
            var form = formation ?? FormationCatalog.GetByName(team.FormationName);

            // Reset transient match state before choosing.
            foreach (var p in team.Players)
            {
                p.AssignedPosition = null;
                p.UsedAsWingBack = false;
                if (p.Status is PlayerStatus.InStartingXI or PlayerStatus.OnBench or PlayerStatus.SubstitutedOff)
                    p.Status = PlayerStatus.Available;
            }

            var eligible = team.Players
                .Where(p => p.Status is not (PlayerStatus.Injured or PlayerStatus.Suspended))
                .ToList();

            var starters = ChooseStarters(eligible, form);

            foreach (var (player, slot) in starters)
            {
                player.Status = PlayerStatus.InStartingXI;
                player.AssignedPosition = slot == player.Position ? null : slot;
                // Keep the explicit WB flag in sync with the AI's own choice, so the UI doesn't
                // show him at his base position while the match engine actually plays him as WB.
                player.UsedAsWingBack = slot is Position.LeftWingBack or Position.RightWingBack;
            }

            var starterIds = starters.Select(s => s.Player.Id).ToHashSet();
            var bench = eligible
                .Where(p => !starterIds.Contains(p.Id))
                .OrderByDescending(p => p.Rating)
                .Take(BenchSize)
                .ToList();

            foreach (var p in bench)
                p.Status = PlayerStatus.OnBench;

            return starters.Select(s => s.Player).ToList();
        }

        private static List<(Player Player, Position Slot)> ChooseStarters(List<Player> eligible, Formation form)
        {
            var chosen = new List<(Player Player, Position Slot)>();
            var used = new HashSet<int>();

            // Keeper first: a real goalkeeper if there is one, else the best available fit.
            int gkSlotIndex = form.Slots.ToList().FindIndex(s => s.Position == Position.Goalkeeper);
            var keeper = eligible
                .Where(p => !used.Contains(p.Id))
                .OrderByDescending(p => p.Position == Position.Goalkeeper ? 1 : 0)
                .ThenByDescending(p => PlayerRoleRating.For(p, Position.Goalkeeper))
                .FirstOrDefault();

            if (keeper is not null)
            {
                chosen.Add((keeper, Position.Goalkeeper));
                used.Add(keeper.Id);
            }

            // Outfield slots: greedy best-fit assignment. Slots with a WingBack alternate role
            // (e.g. LeftDefender/LeftWingBack) are scored under BOTH positions - so a squad's
            // best-fit LAV/RAV specialist can win the slot over a plain LV/RV, exactly like a
            // human toggling the WB button, instead of the AI only ever considering the base role.
            var outfieldSlots = new List<(int Index, Position Pos, Position? AlternatePos)>();
            for (int i = 0; i < form.Slots.Count; i++)
            {
                if (i == gkSlotIndex)
                    continue;
                outfieldSlots.Add((i, form.Slots[i].Position, form.Slots[i].AlternateRole));
            }

            var candidates =
                from p in eligible
                where !used.Contains(p.Id)
                from slot in outfieldSlots
                from candidatePos in slot.AlternatePos is Position alt ? [slot.Pos, alt] : new[] { slot.Pos }
                select (Score: PlayerRoleRating.For(p, candidatePos), Player: p, slot.Index, Pos: candidatePos);

            var filledSlots = new HashSet<int>();
            foreach (var c in candidates.OrderByDescending(c => c.Score))
            {
                if (used.Contains(c.Player.Id) || filledSlots.Contains(c.Index))
                    continue;

                chosen.Add((c.Player, c.Pos));
                used.Add(c.Player.Id);
                filledSlots.Add(c.Index);

                if (filledSlots.Count == outfieldSlots.Count)
                    break;
            }

            return chosen;
        }

        // Matches each formation slot to (at most) one starter, in strict priority order:
        //  1. An EXPLICIT AssignedPosition override (WingBack toggle or manual reposition) -
        //     always wins its designated slot, no matter what.
        //  2. A starter at their own native, unoverridden Position.
        //  3. Any remaining starter (fallback so no slot is left unfilled without reason).
        // Matching purely by "EffectivePosition == slot.Position/AlternateRole" (a single pass)
        // is ambiguous whenever a DIFFERENT starter's native Position happens to equal that same
        // value - e.g. a player generated with Position == LeftWingBack occupying an unrelated
        // midfield slot can "coincidentally" match a LeftDefender slot's AlternateRole and steal
        // it from the player who was actually, explicitly toggled to WingBack duty there. Doing
        // explicit overrides as their own pass first removes that ambiguity entirely.
        public static Dictionary<int, int> MatchStartersToSlots(IReadOnlyList<Player> starters, Formation formation)
        {
            var result = new Dictionary<int, int>();
            var used = new HashSet<int>();

            for (int i = 0; i < formation.Slots.Count; i++)
            {
                var slot = formation.Slots[i];
                var match = starters.FirstOrDefault(p => !used.Contains(p.Id) && p.AssignedPosition is not null &&
                    (p.AssignedPosition == slot.Position || p.AssignedPosition == slot.AlternateRole));
                if (match is null)
                    continue;
                result[i] = match.Id;
                used.Add(match.Id);
            }

            for (int i = 0; i < formation.Slots.Count; i++)
            {
                if (result.ContainsKey(i))
                    continue;
                var slot = formation.Slots[i];
                var match = starters.FirstOrDefault(p => !used.Contains(p.Id) && p.AssignedPosition is null &&
                    (p.Position == slot.Position || p.Position == slot.AlternateRole));
                if (match is null)
                    continue;
                result[i] = match.Id;
                used.Add(match.Id);
            }

            for (int i = 0; i < formation.Slots.Count; i++)
            {
                if (result.ContainsKey(i))
                    continue;
                var match = starters.FirstOrDefault(p => !used.Contains(p.Id));
                if (match is null)
                    continue;
                result[i] = match.Id;
                used.Add(match.Id);
            }

            return result;
        }

        // Non-destructive gap-fill: promotes bench/available players into ONLY the slots that
        // have no current starter mapped to them (by native Position or AssignedPosition/WB
        // role), without touching any existing starter's AssignedPosition. Used as a safety net
        // right before kickoff instead of the destructive SelectLineup, so a manually confirmed
        // lineup (incl. WingBack toggles) is never silently rebuilt team-wide just because one
        // slot happens to be short (e.g. an injury since the Lineup screen was confirmed).
        public static void FillMissingStarters(Team team, Formation formation)
        {
            var starters = team.Players.Where(p => p.Status == PlayerStatus.InStartingXI).ToList();
            if (starters.Count >= formation.Slots.Count)
                return;

            var matched = MatchStartersToSlots(starters, formation);
            var emptySlots = new List<Position>();
            for (int i = 0; i < formation.Slots.Count; i++)
            {
                if (!matched.ContainsKey(i))
                    emptySlots.Add(formation.Slots[i].Position);
            }

            if (emptySlots.Count == 0)
                return;

            var candidates = team.Players
                .Where(p => p.Status is PlayerStatus.Available or PlayerStatus.OnBench)
                .ToList();

            foreach (var slotPos in emptySlots)
            {
                var best = BestForPosition(candidates, slotPos);
                if (best is null)
                    continue;

                candidates.Remove(best);
                best.Status = PlayerStatus.InStartingXI;
                best.AssignedPosition = slotPos == best.Position ? null : slotPos;
                best.UsedAsWingBack = slotPos is Position.LeftWingBack or Position.RightWingBack;
            }
        }

        // Re-applies the manager's/co-trainer's persisted baseline XI+bench (Team.BaselineStartingIds/
        // BaselineBenchIds, set on LineupViewModel.Confirm) - used right after a match so an
        // in-match substitution or red-card reshuffle never leaks into the next matchday's
        // default lineup. A player currently Injured/Suspended is left untouched - the gap this
        // leaves in the XI is covered by FillMissingStarters afterwards, same as any other
        // missing starter. No-op if no baseline has ever been confirmed (old saves, AI teams).
        public static void RestoreBaseline(Team team)
        {
            if (team.BaselineStartingIds.Count == 0 && team.BaselineBenchIds.Count == 0)
                return;

            var byId = team.Players.ToDictionary(p => p.Id);
            bool Eligible(int id) => byId.TryGetValue(id, out var p) &&
                p.Status is not (PlayerStatus.Injured or PlayerStatus.Suspended);

            var starterIds = team.BaselineStartingIds.Where(Eligible).ToHashSet();
            var benchIds = team.BaselineBenchIds.Where(id => Eligible(id) && !starterIds.Contains(id)).ToHashSet();

            foreach (var p in team.Players)
            {
                if (p.Status is PlayerStatus.Injured or PlayerStatus.Suspended)
                    continue;

                if (starterIds.Contains(p.Id))
                    p.Status = PlayerStatus.InStartingXI;
                else if (benchIds.Contains(p.Id))
                {
                    p.Status = PlayerStatus.OnBench;
                    p.AssignedPosition = null;
                }
                else
                {
                    p.Status = PlayerStatus.Available;
                    p.AssignedPosition = null;
                }
            }
        }

        // Tops the baseline bench (and, if a STARTER left, the baseline XI) back up right after
        // a squad departure (sale, loan out, contract expiry) - call once the player has already
        // been removed from team.Players. Only ever fills the vacated slot(s); the rest of the
        // manager's baseline is left exactly as confirmed, unlike the destructive SelectLineup.
        public static void RefillBench(Team team)
        {
            // No baseline confirmed yet (a fresh team, an AI team, or ANY save from before this
            // feature existed) - seed one from whatever is currently live BEFORE touching
            // anything. Without this, an empty BaselineStartingIds looks identical to "nobody
            // should be a starter" to RestoreBaseline below, and calling it would demote the
            // entire live XI/bench to Available instead of just topping up the bench.
            if (team.BaselineStartingIds.Count == 0 && team.BaselineBenchIds.Count == 0)
            {
                team.BaselineStartingIds = team.Players
                    .Where(p => p.Status == PlayerStatus.InStartingXI).Select(p => p.Id).ToList();
                team.BaselineBenchIds = team.Players
                    .Where(p => p.Status == PlayerStatus.OnBench).Select(p => p.Id).ToList();
            }

            var formation = FormationCatalog.GetByName(team.FormationName);
            var rosterIds = team.Players.Select(p => p.Id).ToHashSet();

            var starters = team.BaselineStartingIds.Where(rosterIds.Contains).ToList();
            if (starters.Count < team.BaselineStartingIds.Count)
            {
                team.BaselineStartingIds = starters;
                FillMissingStarters(team, formation);
                team.BaselineStartingIds = team.Players
                    .Where(p => p.Status == PlayerStatus.InStartingXI).Select(p => p.Id).ToList();
            }

            var bench = team.BaselineBenchIds.Where(rosterIds.Contains).ToList();
            var excluded = new HashSet<int>(team.BaselineStartingIds);
            excluded.UnionWith(bench);

            // Rank reserves by how well they fit a position the current formation actually
            // uses (best PlayerRoleRating across those positions) - not flat Rating - so a
            // backup can actually cover a role the team needs instead of just being the
            // highest-rated player left over regardless of position (e.g. filling every open
            // bench slot with strikers while the squad has no spare full-back).
            var formationPositions = formation.Slots
                .SelectMany(s => s.AlternateRole is Position alt ? new[] { s.Position, alt } : new[] { s.Position })
                .Distinct()
                .ToList();

            var reserves = team.Players
                .Where(p => !excluded.Contains(p.Id) && p.Status == PlayerStatus.Available)
                .OrderByDescending(p => formationPositions.Max(pos => PlayerRoleRating.For(p, pos)));

            foreach (var p in reserves)
            {
                if (bench.Count >= BenchSize)
                    break;
                bench.Add(p.Id);
            }

            team.BaselineBenchIds = bench;
            RestoreBaseline(team);
        }

        // Rates how well the squad fits a formation (sum of the best XI's rating × position
        // fit) WITHOUT mutating anything - used to recommend a formation. Higher = better.
        public static double ScoreFormation(Team team, Formation formation)
        {
            var eligible = team.Players
                .Where(p => p.Status is not (PlayerStatus.Injured or PlayerStatus.Suspended))
                .ToList();
            var chosen = ChooseStarters(eligible, formation);
            return chosen.Sum(c => PlayerRoleRating.For(c.Player, c.Slot));
        }

        // Best replacement from a set of candidates for a given position (used for subs).
        public static Player? BestForPosition(IEnumerable<Player> candidates, Position position) =>
            candidates
                .Where(p => p.Status is not (PlayerStatus.Injured or PlayerStatus.Suspended))
                .OrderByDescending(p => PlayerRoleRating.For(p, position))
                .FirstOrDefault();
    }
}
