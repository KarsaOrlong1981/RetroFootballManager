namespace RetroFootballManager.Models
{
    // Used to pick the correct portrait pool for staff (see FaceImageAssigner) - players
    // always use the male-only NewGen pool, so they don't need this.
    public enum Gender
    {
        Male,
        Female,
    }
}
