using RetroFootballManager.Models;

namespace RetroFootballManager.Core.Models
{
    public static class PersonalityDisplay
    {
        public static string Name(Personality personality) => personality switch
        {
            Personality.None => "Keine",
            Personality.Maestro => "Spielmacher",
            Personality.Hothead => "Hitzkopf",
            Personality.Workhorse => "Arbeiter",
            Personality.Sprinter => "Sprinter",
            Personality.Strategist => "Stratege",
            Personality.Leader => "Anführer",
            Personality.Technician => "Techniker",
            Personality.Enforcer => "Abräumer",
            Personality.HeaderBeast => "Kopfballungeheuer",
            _ => "Unbekannt",
        };
    }
}
