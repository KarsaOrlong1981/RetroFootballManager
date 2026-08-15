using RetroFootballManager.Models;

namespace RetroFootballManager.Core.Models
{
    // German display labels for InMatchCharacterType. Deliberately diverges from
    // PersonalityDisplay's labels where the two enums share a name (Leader, Hothead) so the
    // UI never shows the same word for two unrelated systems - see InMatchCharacterType's own
    // doc comment.
    public static class InMatchCharacterDisplay
    {
        public static string Name(InMatchCharacterType? type) => type switch
        {
            InMatchCharacterType.Fighter => "Kämpfer",
            InMatchCharacterType.ClutchPerformer => "Big-Game-Player",
            InMatchCharacterType.Leader => "Vorbild",
            InMatchCharacterType.MomentumHunter => "Wellenreiter",
            InMatchCharacterType.IceCold => "Eiskalt",
            InMatchCharacterType.NervousUnderPressure => "Nervös unter Druck",
            InMatchCharacterType.Complacent => "Selbstzufrieden",
            InMatchCharacterType.Hothead => "Pulverfass",
            InMatchCharacterType.FragileConfidence => "Labiles Selbstvertrauen",
            InMatchCharacterType.LazyWhenLeading => "Bequem in Führung",
            InMatchCharacterType.Emotional => "Emotional",
            InMatchCharacterType.RiskTaker => "Draufgänger",
            InMatchCharacterType.CrowdDriven => "Publikumsliebling",
            InMatchCharacterType.SlowStarter => "Spätzünder",
            InMatchCharacterType.MomentumSensitive => "Stimmungsabhängig",
            _ => "Unbekannt",
        };
    }
}
