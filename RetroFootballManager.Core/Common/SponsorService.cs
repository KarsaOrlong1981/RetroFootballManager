using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Offers sponsors matching the league tier (kit sponsor only from tier 2 or better) and
    // signs deals - a team has at most one active deal per slot (main/board/kit).
    public class SponsorService
    {
        private readonly SponsorRepository _sponsors;
        private readonly SponsorshipRepository _sponsorships;

        public SponsorService(SponsorRepository sponsors, SponsorshipRepository sponsorships)
        {
            _sponsors = sponsors;
            _sponsorships = sponsorships;
        }

        public async Task<List<Sponsor>> GetAvailableOffersAsync(Team team, SponsorType slot)
        {
            if (slot == SponsorType.Kit && team.LeagueTier > 2)
                return [];

            var catalog = await _sponsors.GetAllAsync();
            return catalog
                .Where(s => s.SponsorType == slot && team.LeagueTier <= s.MinTier)
                .OrderByDescending(s => s.SeasonPayment)
                .ToList();
        }

        // Blocks signing while the current deal in this slot is still running -
        // otherwise a sponsor could be replaced instantly/arbitrarily often.
        // Returns null if blocked (contract still active).
        public async Task<Sponsorship?> SignAsync(Team team, Sponsor sponsor, int currentSeason, int durationSeasons = 2)
        {
            var existing = await _sponsorships.GetByTeamAsync(team.Id);
            var current = existing.FirstOrDefault(s => s.SponsorType == sponsor.SponsorType);

            if (current is not null && currentSeason < current.StartSeason + current.Duration)
                return null;

            var sponsorship = current ?? new Sponsorship { TeamId = team.Id, SponsorType = sponsor.SponsorType };
            sponsorship.SponsorId = sponsor.Id;
            sponsorship.StartSeason = currentSeason;
            sponsorship.Duration = durationSeasons;

            await _sponsorships.SaveAsync(sponsorship);
            return sponsorship;
        }
    }
}
