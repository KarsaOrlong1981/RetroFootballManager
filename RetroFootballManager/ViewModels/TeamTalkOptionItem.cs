using RetroFootballManager.Common;
using RetroFootballManager.Models;

namespace RetroFootballManager.ViewModels
{
    // One selectable option in the half-time team-talk dialog.
    public record TeamTalkOptionItem(TeamTalkOption Option, string Label)
    {
        public static readonly IReadOnlyList<TeamTalkOptionItem> All =
            Enum.GetValues<TeamTalkOption>()
                .Select(o => new TeamTalkOptionItem(o, TeamTalkDisplay.Label(o)))
                .ToList();
    }
}
