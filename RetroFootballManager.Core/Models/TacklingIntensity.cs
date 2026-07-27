namespace RetroFootballManager.Models
{
    // How hard a team/player goes into tackles - independent of the base tactic.
    // The team setting is the default; a player can be overridden individually at
    // any time (even mid-match, e.g. after a yellow card).
    public enum TacklingIntensity
    {
        Cautious,
        Normal,
        Moderate,
        Hard,
    }
}
