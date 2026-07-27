using RetroFootballManager.Models;

namespace RetroFootballManager.Core.Models
{
    public static class PositionDisplay
    {
        public static string Short(Position position) => position switch
        {
            Position.CentralDefender => "IV",
            Position.LeftDefender => "LV",
            Position.RightDefender => "RV",
            Position.LeftWingBack => "LAV",
            Position.RightWingBack => "RAV",
            Position.DefensiveMidfielder => "DM",
            Position.CentralMidfielder => "ZM",
            Position.CentralOffenseMidfielder => "ZOM",
            Position.LeftMidfielder => "LM",
            Position.RightMidfielder => "RM",
            Position.LeftOffenseMidfielder => "LA",
            Position.RightOffenseMidfielder => "RA",
            Position.Forward => "ST",
            _ => "TW",
        };
    }
}
