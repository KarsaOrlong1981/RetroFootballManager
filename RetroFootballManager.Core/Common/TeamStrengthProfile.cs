namespace RetroFootballManager.Common
{
    // Result of a team's strength calculation for a specific match.
    public readonly record struct TeamStrengthProfile(
        double Overall,
        double Attack,
        double Defense,
        double Midfield,
        double Pressing,
        double DisciplineRisk);
}
