namespace RetroFootballManager.ViewModels.Messages
{
    // Sent whenever a player's live state (Moral, Fitness, WantsToLeaveClub, ...) changes outside
    // of a currently open PlayerProfileDialog - e.g. a talk on TalkToPlayerPage - so any page
    // still showing that player's (snapshot) PlayerProfile can refresh it.
    public sealed record PlayerProfileChangedMessage(int PlayerId);
}
