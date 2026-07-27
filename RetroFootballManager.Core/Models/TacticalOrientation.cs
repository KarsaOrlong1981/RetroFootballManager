namespace RetroFootballManager.Models
{
    // How defensive/offensive a team plays overall - a dial applied on top of the chosen
    // PlayingStyle's factors (analogous to TacklingIntensity as its own axis).
    public enum TacticalOrientation
    {
        VeryDefensive,
        Defensive,
        Balanced,
        Offensive,
        VeryOffensive,
    }
}
