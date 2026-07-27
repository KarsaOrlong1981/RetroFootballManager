namespace RetroFootballManager.Models
{
    // Team-wide training emphasis: slowly improves 1-2 relevant attributes across the whole
    // squad over many weeks (separate from an individual player's own training focus).
    public enum TeamTrainingFocus
    {
        Pressing,
        CrossesToStriker,
        TikiTaka,
        CounterAttack,
        Offensive,
        Defensive,
        WingPlay,
    }
}
