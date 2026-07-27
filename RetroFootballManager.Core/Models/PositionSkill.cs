namespace RetroFootballManager.Models
{
    // How well a player can perform at a position other than his main one.
    // Proficiency: 0-99, the higher the value the smaller the penalty vs. the main position.
    public record PositionSkill(Position Position, int Proficiency);
}
