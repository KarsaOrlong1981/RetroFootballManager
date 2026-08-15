using RetroFootballManager.Models;

namespace RetroFootballManager.ViewModels
{
    public record PlayingStyleOption(PlayingStyle Style, string Label)
    {
        public static string LabelFor(PlayingStyle s) => s switch
        {
            PlayingStyle.CounterAttack => "Konter",
            PlayingStyle.TikiTaka => "Ballbesitz",
            PlayingStyle.Pressing => "Pressing",
            PlayingStyle.WingPlay => "Flügelspiel",
            PlayingStyle.CrossesToStriker => "Flanken auf Stürmer",
            _ => s.ToString(),
        };
    }

    public record OrientationOption(TacticalOrientation Orientation, string Label)
    {
        public static string LabelFor(TacticalOrientation o) => o switch
        {
            TacticalOrientation.VeryDefensive => "Sehr Defensiv",
            TacticalOrientation.Defensive => "Defensiv",
            TacticalOrientation.Balanced => "Ausgeglichen",
            TacticalOrientation.Offensive => "Offensiv",
            TacticalOrientation.VeryOffensive => "Sehr Offensiv",
            _ => o.ToString(),
        };
    }
}
