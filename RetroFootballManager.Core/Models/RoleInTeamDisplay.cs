namespace RetroFootballManager.Models
{
    public static class RoleInTeamDisplay
    {
        public static string Label(RoleInTeam role) => role switch
        {
            RoleInTeam.KeyPlayer => "Schlüsselspieler",
            RoleInTeam.RotationPlayer => "Rotationsspieler",
            RoleInTeam.Backup => "Backup",
            RoleInTeam.FutureTalent => "Talent für die Zukunft",
            _ => role.ToString(),
        };
    }
}
