using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public record MatchEvent(
        int Minute,
        GameEventType Type,
        bool IsHomeTeam,
        Player? Player,
        string Description);
}
