using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Which positions are naturally related, so a player can plausibly cover them as a
    // secondary position. Used to generate versatile players and to keep lineup choices
    // sensible. Goalkeepers stay goalkeepers (no related outfield positions).
    public static class PositionRelations
    {
        private static readonly Dictionary<Position, Position[]> Related = new()
        {
            [Position.Goalkeeper] = [],
            [Position.CentralDefender] = [Position.LeftDefender, Position.RightDefender, Position.DefensiveMidfielder],
            [Position.LeftDefender] = [Position.LeftWingBack, Position.LeftOffenseMidfielder, Position.LeftMidfielder],
            [Position.RightDefender] = [Position.RightWingBack, Position.RightOffenseMidfielder, Position.RightMidfielder],
            [Position.LeftWingBack] = [Position.LeftDefender, Position.LeftMidfielder, Position.LeftOffenseMidfielder],
            [Position.RightWingBack] = [Position.RightDefender, Position.RightMidfielder, Position.RightOffenseMidfielder],
            [Position.DefensiveMidfielder] = [Position.CentralMidfielder, Position.CentralDefender],
            [Position.CentralMidfielder] = [Position.DefensiveMidfielder, Position.CentralOffenseMidfielder, Position.LeftMidfielder, Position.RightMidfielder],
            [Position.LeftMidfielder] = [Position.LeftOffenseMidfielder, Position.LeftWingBack, Position.CentralMidfielder],
            [Position.RightMidfielder] = [Position.RightOffenseMidfielder, Position.RightWingBack, Position.CentralMidfielder],
            [Position.CentralOffenseMidfielder] = [Position.CentralMidfielder, Position.Forward, Position.LeftOffenseMidfielder, Position.RightOffenseMidfielder],
            [Position.LeftOffenseMidfielder] = [Position.LeftMidfielder, Position.CentralOffenseMidfielder, Position.Forward],
            [Position.RightOffenseMidfielder] = [Position.RightMidfielder, Position.CentralOffenseMidfielder, Position.Forward],
            [Position.Forward] = [Position.CentralOffenseMidfielder, Position.LeftOffenseMidfielder, Position.RightOffenseMidfielder],
        };

        public static IReadOnlyList<Position> GetRelated(Position position) =>
            Related.TryGetValue(position, out var list) ? list : [];
    }
}
