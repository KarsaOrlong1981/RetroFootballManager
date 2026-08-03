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

        public event EventHandler<EventArgs>? SponsorChanged;

        public SponsorService(SponsorRepository sponsors, SponsorshipRepository sponsorships)
        {
            _sponsors = sponsors;
            _sponsorships = sponsorships;
        }

        public async Task<List<Sponsor>> GetAvailableOffersAsync(Team team, SponsorType slot)
        {
            // Tier 3 and Tier 4 get no Kit sponsor offers
            if (slot == SponsorType.Kit && team.LeagueTier > 2)
                return [];

            var catalog = await _sponsors.GetAllAsync();
            var sponsorWithoutBonus = catalog
                    .Where(s =>
                        s.SponsorType == slot &&
                        s.MinTier == team.LeagueTier &&
                        s.HasNoBonus)
                    .OrderByDescending(s => s.SeasonPayment)
                    .FirstOrDefault();
            var bonusSponsors = catalog
                     .Where(s =>
                         s.SponsorType == slot &&
                         s.MinTier == team.LeagueTier &&
                         !s.HasNoBonus)
                     .OrderByDescending(s => s.SeasonPayment)
                     .Take(2)
                     .ToList();
            var result = new List<Sponsor>();
            if (sponsorWithoutBonus != null)
                result.Add(sponsorWithoutBonus);

            result.AddRange(bonusSponsors);

            return result;
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
            SponsorChanged?.Invoke(this, EventArgs.Empty);
            return sponsorship;
        }

        public async Task<List<(Sponsorship Deal, Sponsor Sponsor)>> GetActiveSponsorshipsAsync(int teamId)
        {
            var sponsorships = await _sponsorships.GetByTeamAsync(teamId);
            var catalog = await _sponsors.GetAllAsync();
            return sponsorships
                .Select(s => (Deal: s, Sponsor: catalog.FirstOrDefault(c => c.Id == s.SponsorId)))
                .Where(x => x.Sponsor is not null)
                .Select(x => (x.Deal, x.Sponsor!))
                .ToList();
        }
    }
}
