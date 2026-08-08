using RetroFootballManager.Models;

namespace RetroFootballManager.ViewModels
{
    public record AttributeChip(string Label, int Value);

    public record PlayerAttributeSummary(IReadOnlyList<AttributeChip> Chips)
    {
        public static PlayerAttributeSummary From(Player p) => new(
            p.Position == Position.Goalkeeper
                ? [
                    new("REF", p.GkReflexes),
                    new("BAL", p.GkHandling),
                    new("1v1", p.GkOneOnOne),
                    new("SPO", p.GkDistribution),
                    new("HER", p.GkAerialControl),
                    new("INT", p.GameIntelligence),
                    new("PAS", p.PassingAccuracy),
                    new("FIT", p.Fitness),
                    new("GRU", p.BaseFitness),
                ]
                : [
                    new("OFF", p.OffensivePower),
                    new("ABS", p.Finishing),
                    new("DEF", p.DefensivePower),
                    new("INT", p.GameIntelligence),
                    new("PRE", p.PressingIntensity),
                    new("TEM", p.CounterSpeed),
                    new("PAS", p.PassingAccuracy),
                    new("ZKH", p.DuelHardness),
                    new("ZKE", p.DuelEfficiency),
                    new("FLA", p.CrossingAccuracy),
                    new("KOP", p.HeaderStrength),
                    new("SPR", p.Jumping),
                    new("DRI", p.Dribbling),
                    new("WEI", p.LongShotAccuracy),
                    new("ELF", p.PenaltyKick),
                    new("FRS", p.FreeKick),
                    new("FIT", p.Fitness),
                    new("GRU", p.BaseFitness),
                ]);
    }
}
