using RetroFootballManager.Core.Models;
using RetroFootballManager.Models;

namespace RetroFootballManager.ViewModels
{
    // Full player profile shown in a dialog when tapping a player's info button.
    public record PlayerProfile(
        string Name,
        string Position,
        string SecondaryPositions,
        int Age,
        string DateOfBirth,
        string Nationality,
        string Personality,
        double Rating,
        int Talent,
        int Moral,
        int Fitness,
        double Size,
        string Status,
        IReadOnlyList<AttributeChip> Attributes,
        int CareerAppearances,
        int CareerMinutesPlayed,
        int SeasonMinutes,
        bool IsYouthProspect,
        string ContractNote,
        string TransferStatusNote,
        int Goals,
        int Assists,
        int YellowCards,
        int RedCards)
    {
        public static PlayerProfile From(
            Player p, Contract? contract = null, TransferListing? listing = null, PlayerStats? seasonStats = null) => new(
            p.Name,
            p.ShortPositionName,
            p.SecondaryPositions.Count == 0
                ? "Keine"
                : string.Join(", ", p.SecondaryPositions.Select(sp => $"{PositionDisplay.Short(sp.Position)} ({sp.Proficiency})")),
            p.Age,
            p.DateOfBirth.ToString("dd.MM.yyyy"),
            p.Nationality.ToString(),
            p.Personality.ToString(),
            p.Rating,
            p.Talent,
            p.Moral,
            p.Fitness,
            p.Size,
            StatusLabel(p.Status),
            PlayerAttributeSummary.From(p).Chips,
            p.CareerAppearances,
            p.CareerMinutesPlayed,
            p.SeasonMinutes,
            p.IsYouthProspect,
            ContractText(contract),
            TransferStatusText(listing),
            seasonStats?.Goals ?? 0,
            seasonStats?.Assists ?? 0,
            seasonStats?.YellowCards ?? 0,
            seasonStats?.RedCards ?? 0);

        private static string ContractText(Contract? c) => c is null
            ? "Kein aktiver Vertrag."
            : $"Vertrag bis {c.EndDate:MMMM yyyy} · Gehalt {c.AnnualSalary:N0} €/Jahr";

        private static string TransferStatusText(TransferListing? listing)
        {
            if (listing is null)
                return "Nicht angeboten.";
            if (listing.IsUnsolicited)
                return "Ein Angebot liegt vor - der Verein entscheidet, ob er verkauft.";
            return listing.IsLoanListing
                ? $"Zur Leihe angeboten (Konditionen: {listing.AskingPrice:N0} €)"
                : $"Zum Transfer angeboten (Preisvorstellung: {listing.AskingPrice:N0} €)";
        }

        private static string StatusLabel(PlayerStatus status) => status switch
        {
            PlayerStatus.InStartingXI => "Startelf",
            PlayerStatus.OnBench => "Bank",
            PlayerStatus.Injured => "Verletzt",
            PlayerStatus.Suspended => "Gesperrt",
            PlayerStatus.SubstitutedOff => "Ausgewechselt",
            _ => "Reserve",
        };
    }
}
