namespace RetroFootballManager.Common
{
    // Controls a team during a live match (tactics, substitutions).
    // Called by the engine after every simulated minute.
    public interface IMatchCoach
    {
        void OnMinute(Match match, bool isHome, int minute);
    }
}
