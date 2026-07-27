using RetroFootballManager.Models;

namespace RetroFootballManager.Services
{
    public class GameSession
    {
        public GameState? State { get; set; }

        public List<Team> Teams { get; set; } = [];

        // manager Team
        public Team? ManagerTeam =>
            State is null ? null : Teams.FirstOrDefault(t => t.Id == State.ManagerTeamId);

        public (List<League> Leagues, List<Team> Teams)? PendingUniverse { get; set; }

        public bool HasActiveGame => State is not null;

        public void Clear()
        {
            State = null;
            Teams = [];
            PendingUniverse = null;
        }
    }
}
