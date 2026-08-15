using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // German display labels for TeamTalkOption - pure text formatting, no MAUI dependency
    // (same reasoning as PreMatchAnalysisService's own small switch label).
    public static class TeamTalkDisplay
    {
        public static string Label(TeamTalkOption option) => option switch
        {
            TeamTalkOption.StayCalm => "Ruhig bleiben",
            TeamTalkOption.CheerOn => "Anfeuern",
            TeamTalkOption.Praise => "Loben",
            TeamTalkOption.Criticize => "Kritisieren",
            TeamTalkOption.Shout => "Anschreien",
            TeamTalkOption.TacticalTalk => "Taktische Ansage",
            TeamTalkOption.ExpressConfidence => "Vertrauen aussprechen",
            TeamTalkOption.WarnAgainstComplacency => "Vor Nachlässigkeit warnen",
            TeamTalkOption.EmotionalBuildUp => "Emotional aufbauen",
            TeamTalkOption.SayNothing => "Nichts sagen",
            _ => option.ToString(),
        };
    }
}
