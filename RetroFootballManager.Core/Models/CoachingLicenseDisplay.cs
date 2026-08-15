using RetroFootballManager.Models;

namespace RetroFootballManager.Core.Models
{
    public static class CoachingLicenseDisplay
    {
        public static string Label(CoachingLicense license) => license switch
        {
            CoachingLicense.C => "C-Lizenz",
            CoachingLicense.B => "B-Lizenz",
            CoachingLicense.A => "A-Lizenz",
            CoachingLicense.Pro => "Pro-Lizenz",
            _ => license.ToString(),
        };
    }
}
