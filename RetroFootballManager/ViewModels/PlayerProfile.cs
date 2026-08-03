using RetroFootballManager.Common;
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
        int RedCards,
        double MarketValue,
        int CareerGoals,
        int CareerAssists,
        int CareerYellowCards,
        int CareerRedCards)
    {
        public static PlayerProfile From(
            Player p, Contract? contract = null, TransferListing? listing = null,
            PlayerStats? seasonStats = null, PlayerStats? careerStats = null) => new(
            p.Name,
            p.ShortPositionName,
            p.SecondaryPositions.Count == 0
                ? "Keine"
                : string.Join(", ", p.SecondaryPositions.Select(sp => $"{PositionDisplay.Short(sp.Position)} ({sp.Proficiency})")),
            p.Age,
            p.DateOfBirth.ToString("dd.MM.yyyy"),
            p.Nationality.ToString(),
            GetPersonalityTranslation(p.Personality),
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
            seasonStats?.RedCards ?? 0,
            PlayerValuationService.EstimateMarketValue(p),
            careerStats?.Goals ?? 0,
            careerStats?.Assists ?? 0,
            careerStats?.YellowCards ?? 0,
            careerStats?.RedCards ?? 0);

        private static string ContractText(Contract? c) => c is null
            ? "Kein aktiver Vertrag."
            : $"Vertrag bis {c.EndDate:MMMM yyyy} · Gehalt {c.AnnualSalary:N0} €/Jahr";

        private static string GetPersonalityTranslation(Personality personality)
        {
            return personality switch
            {
                Models.Personality.None => "Keine",
                Models.Personality.Maestro => "Spielmacher",
                Models.Personality.Hothead => "Hitzkopf",
                Models.Personality.Workhorse => "Arbeiter",
                Models.Personality.Sprinter => "Sprinter",
                Models.Personality.Strategist => "Stratege",
                Models.Personality.Leader => "Anführer",
                Models.Personality.Technician => "Techniker",
                Models.Personality.Enforcer => "Abräumer",
                Models.Personality.HeaderBeast => "Kopfballungeheuer",
                _ => "Unbekannt"
            };
        }

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
