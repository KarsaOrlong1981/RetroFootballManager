using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // A slot on the pitch: its position and normalised coordinates (0..1). X = left→right,
    // Y = 0 (opponent's goal, top of the pitch view) → 1 (own goal / goalkeeper, bottom of
    // the pitch view) - matches how a manager views their own half at the bottom.
    //
    // AlternateRole: some wide slots can be played either as a "normal" full-back/midfielder
    // OR pushed into a wing-back role (more advanced, more crossing-focused). The manager
    // decides per slot in the lineup screen; null means the slot has no alternate.
    public record FormationSlot(Position Position, double X, double Y, Position? AlternateRole = null);

    // A formation as 11 ordered slots (index 0 is always the keeper).
    public record Formation(string Name, IReadOnlyList<FormationSlot> Slots)
    {
        public IReadOnlyList<Position> Positions => Slots.Select(s => s.Position).ToList();
    }

    public static class FormationCatalog
    {
        public static readonly Formation F442 = new("4-4-2",
        [
            new(Position.Goalkeeper, 0.50, 0.93),
            new(Position.LeftDefender, 0.18, 0.72, Position.LeftWingBack), new(Position.CentralDefender, 0.40, 0.77),
            new(Position.CentralDefender, 0.60, 0.77), new(Position.RightDefender, 0.82, 0.72, Position.RightWingBack),
            new(Position.LeftMidfielder, 0.18, 0.45), new(Position.CentralMidfielder, 0.40, 0.48),
            new(Position.CentralMidfielder, 0.60, 0.48), new(Position.RightMidfielder, 0.82, 0.45),
            new(Position.Forward, 0.38, 0.15), new(Position.Forward, 0.62, 0.15),
        ]);

        public static readonly Formation F433 = new("4-3-3",
        [
            new(Position.Goalkeeper, 0.50, 0.93),
            new(Position.LeftDefender, 0.18, 0.72, Position.LeftWingBack), new(Position.CentralDefender, 0.40, 0.77),
            new(Position.CentralDefender, 0.60, 0.77), new(Position.RightDefender, 0.82, 0.72, Position.RightWingBack),
            new(Position.CentralMidfielder, 0.30, 0.48), new(Position.DefensiveMidfielder, 0.50, 0.50),
            new(Position.CentralMidfielder, 0.70, 0.48),
            new(Position.LeftOffenseMidfielder, 0.20, 0.18), new(Position.Forward, 0.50, 0.14),
            new(Position.RightOffenseMidfielder, 0.80, 0.18),
        ]);

        public static readonly Formation F4231 = new("4-2-3-1",
        [
            new(Position.Goalkeeper, 0.50, 0.93),
            new(Position.LeftDefender, 0.18, 0.72, Position.LeftWingBack), new(Position.CentralDefender, 0.40, 0.77),
            new(Position.CentralDefender, 0.60, 0.77), new(Position.RightDefender, 0.82, 0.72, Position.RightWingBack),
            new(Position.CentralMidfielder, 0.38, 0.55), new(Position.CentralMidfielder, 0.62, 0.55),
            new(Position.LeftOffenseMidfielder, 0.20, 0.32), new(Position.CentralOffenseMidfielder, 0.50, 0.34),
            new(Position.RightOffenseMidfielder, 0.80, 0.32),
            new(Position.Forward, 0.50, 0.12),
        ]);

        // Note on spacing (also below for F4222/F352/F4312): these slots got extra breathing
        // room between the lone holding mid and its neighbours, and the forward pair got
        // widened - at the small pitch-token size used in the "Mannschaft anpassen" dialogs
        // (see App.xaml PitchBoundsSmall), the original tighter deltas caused the tokens to
        // visually overlap each other. Other formations weren't touched - their spacing was
        // already wide enough.
        public static readonly Formation F4141 = new("4-1-4-1",
        [
            new(Position.Goalkeeper, 0.50, 0.93),

            new(Position.LeftDefender, 0.18, 0.72, Position.LeftWingBack),
            new(Position.CentralDefender, 0.40, 0.80),
            new(Position.CentralDefender, 0.60, 0.80),
            new(Position.RightDefender, 0.82, 0.72, Position.RightWingBack),

            new(Position.DefensiveMidfielder, 0.50, 0.63), // lone holding midfielder

            new(Position.LeftMidfielder, 0.18, 0.42),
            new(Position.CentralMidfielder, 0.40, 0.45),
            new(Position.CentralMidfielder, 0.60, 0.45),
            new(Position.RightMidfielder, 0.82, 0.42),

            new(Position.Forward, 0.50, 0.12),
        ]);

        // The deepest midfield pair is only a genuine double-pivot (both DM) when the team
        // plays defensively/balanced; an offensive orientation pushes both into box-to-box
        // CM roles instead. See GetByName(name, orientation).
        public static Formation BuildF4222(TacticalOrientation orientation)
        {
            var pivot = IsOffensive(orientation) ? Position.CentralMidfielder : Position.DefensiveMidfielder;
            return new Formation("4-2-2-2",
            [
                new(Position.Goalkeeper, 0.50, 0.93),

                new(Position.LeftDefender, 0.18, 0.72, Position.LeftWingBack),
                new(Position.CentralDefender, 0.40, 0.77),
                new(Position.CentralDefender, 0.60, 0.77),
                new(Position.RightDefender, 0.82, 0.72, Position.RightWingBack),

                new(pivot, 0.38, 0.55),
                new(pivot, 0.62, 0.55),

                new(Position.LeftOffenseMidfielder, 0.20, 0.32),
                new(Position.RightOffenseMidfielder, 0.80, 0.32),

                new(Position.Forward, 0.38, 0.12),
                new(Position.Forward, 0.62, 0.12),
            ]);
        }

        private static bool IsOffensive(TacticalOrientation orientation) =>
            orientation is TacticalOrientation.Offensive or TacticalOrientation.VeryOffensive;

        public static readonly Formation F4222 = BuildF4222(TacticalOrientation.Balanced);

        // Back-3 system: no wide defenders. The AV/WB toggle only ever applies to LV/RV
        // slots, so a 3-5-2 simply has no wing-back alternate - its wide players are plain
        // LM/RM.
        public static readonly Formation F352 = new("3-5-2",
        [
            new(Position.Goalkeeper, 0.50, 0.93),

            new(Position.CentralDefender, 0.28, 0.75),
            new(Position.CentralDefender, 0.50, 0.78),
            new(Position.CentralDefender, 0.72, 0.75),

            new(Position.LeftMidfielder, 0.15, 0.40),
            new(Position.CentralMidfielder, 0.32, 0.46),
            new(Position.DefensiveMidfielder, 0.50, 0.60),
            new(Position.CentralMidfielder, 0.68, 0.46),
            new(Position.RightMidfielder, 0.85, 0.40),

            new(Position.Forward, 0.38, 0.12),
            new(Position.Forward, 0.62, 0.12),
        ]);

        public static readonly Formation F4312 = new("4-3-1-2",
        [
            new(Position.Goalkeeper, 0.50, 0.93),

            new(Position.LeftDefender, 0.18, 0.72, Position.LeftWingBack),
            new(Position.CentralDefender, 0.40, 0.77),
            new(Position.CentralDefender, 0.60, 0.77),
            new(Position.RightDefender, 0.82, 0.72, Position.RightWingBack),

            new(Position.CentralMidfielder, 0.24, 0.48),
            new(Position.DefensiveMidfielder, 0.50, 0.56),
            new(Position.CentralMidfielder, 0.76, 0.48),

            new(Position.CentralOffenseMidfielder, 0.50, 0.32),

            new(Position.Forward, 0.38, 0.12),
            new(Position.Forward, 0.62, 0.12),
        ]);

        public static readonly Formation F4321 = new("4-3-2-1",
        [
            new(Position.Goalkeeper, 0.50, 0.93),

            new(Position.LeftDefender, 0.18, 0.72, Position.LeftWingBack),
            new(Position.CentralDefender, 0.40, 0.77),
            new(Position.CentralDefender, 0.60, 0.77),
            new(Position.RightDefender, 0.82, 0.72, Position.RightWingBack),

            new(Position.CentralMidfielder, 0.30, 0.50),
            new(Position.DefensiveMidfielder, 0.50, 0.52),
            new(Position.CentralMidfielder, 0.70, 0.50),

            new(Position.LeftOffenseMidfielder, 0.35, 0.30),
            new(Position.RightOffenseMidfielder, 0.65, 0.30),

            new(Position.Forward, 0.50, 0.10),
         ]);

        public static readonly IReadOnlyList<Formation> All =
        [
            F442, F433, F4231,
            F4141, F4222, F352, F4312, F4321
        ];

        public static Formation Default => F442;

        public static Formation GetByName(string? name) =>
            All.FirstOrDefault(f => f.Name == name) ?? Default;

        // 4-2-2-2's pivot depends on the team's tactical orientation - resolve it dynamically
        // instead of returning the static (Balanced-shaped) F4222 field.
        public static Formation GetByName(string? name, TacticalOrientation orientation) =>
            name == F4222.Name ? BuildF4222(orientation) : GetByName(name);
    }
}
