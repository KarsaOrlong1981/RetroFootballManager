namespace RetroFootballManager.Models
{
    // Governs how fast COM teams develop (individual + team training). The human manager's
    // own training pace is unaffected - this only scales the AI's weekly progress.
    public enum Difficulty
    {
        Easy,
        Normal,
        Hard,
    }
}
