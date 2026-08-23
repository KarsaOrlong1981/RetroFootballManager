namespace RetroFootballManager.Models
{
    public static class PlayerAttributeTypeDisplay
    {
        public static string Label(PlayerAttributeType type) => type switch
        {
            PlayerAttributeType.OffensivePower => "Offensivkraft",
            PlayerAttributeType.DefensivePower => "Defensivkraft",
            PlayerAttributeType.GameIntelligence => "Spielintelligenz",
            PlayerAttributeType.PressingIntensity => "Pressingintensität",
            PlayerAttributeType.CounterSpeed => "Konter-Tempo",
            PlayerAttributeType.PassingAccuracy => "Passgenauigkeit",
            PlayerAttributeType.DuelHardness => "Zweikampfhärte",
            PlayerAttributeType.DuelEfficiency => "Zweikampfstärke",
            PlayerAttributeType.CrossingAccuracy => "Flankengenauigkeit",
            PlayerAttributeType.HeaderStrength => "Kopfballstärke",
            PlayerAttributeType.Jumping => "Sprungkraft",
            PlayerAttributeType.Dribbling => "Dribbling",
            PlayerAttributeType.LongShotAccuracy => "Fernschussgenauigkeit",
            PlayerAttributeType.PenaltyKick => "Elfmeterschütze",
            PlayerAttributeType.FreeKick => "Freistoßschütze",
            PlayerAttributeType.Finishing => "Abschlussstärke",
            PlayerAttributeType.Positioning => "Stellungsspiel",
            PlayerAttributeType.GkReflexes => "Torwart-Reflexe",
            PlayerAttributeType.GkHandling => "Ballsicherheit (Torwart)",
            PlayerAttributeType.GkOneOnOne => "1-gegen-1-Stärke (Torwart)",
            PlayerAttributeType.GkDistribution => "Spieleröffnung (Torwart)",
            PlayerAttributeType.GkAerialControl => "Luftraumbeherrschung (Torwart)",
            _ => type.ToString(),
        };
    }
}
