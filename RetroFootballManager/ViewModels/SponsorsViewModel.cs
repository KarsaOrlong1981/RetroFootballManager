using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Common;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Logging;
using RetroFootballManager.Models;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public partial class SponsorsViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<SponsorsViewModel>();

        private readonly GameSession _session;
        private readonly SponsorService _sponsorService;
        private readonly SponsorshipRepository _sponsorshipRepository;
        private readonly SponsorRepository _sponsorRepository;

        private Team? _team;

        public SponsorsViewModel(
            IDispatcher dispatcher, GameSession session, SponsorService sponsorService,
            SponsorshipRepository sponsorshipRepository, SponsorRepository sponsorRepository)
            : base(dispatcher)
        {
            _session = session;
            _sponsorService = sponsorService;
            _sponsorshipRepository = sponsorshipRepository;
            _sponsorRepository = sponsorRepository;
            Title = "Sponsoren";
        }

        public ObservableCollection<Sponsor> MainOffers { get; } = [];
        public ObservableCollection<Sponsor> PerimeterOffers { get; } = [];
        public ObservableCollection<Sponsor> KitOffers { get; } = [];

        [ObservableProperty] private string _currentMainText = string.Empty;
        [ObservableProperty] private string _currentPerimeterText = string.Empty;
        [ObservableProperty] private string _currentKitText = string.Empty;
        [ObservableProperty] private bool _kitSlotAvailable;
        [ObservableProperty] private string _statusText = string.Empty;

        public async Task InitializeAsync()
        {
            _team = _session.ManagerTeam;
            if (_team is null)
                return;

            KitSlotAvailable = _team.LeagueTier <= 2;
            await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            if (_team is null)
                return;

            MainOffers.Clear();
            foreach (var s in await _sponsorService.GetAvailableOffersAsync(_team, SponsorType.Main))
                MainOffers.Add(s);

            PerimeterOffers.Clear();
            foreach (var s in await _sponsorService.GetAvailableOffersAsync(_team, SponsorType.Perimeter))
                PerimeterOffers.Add(s);

            KitOffers.Clear();
            foreach (var s in await _sponsorService.GetAvailableOffersAsync(_team, SponsorType.Kit))
                KitOffers.Add(s);

            var current = await _sponsorshipRepository.GetByTeamAsync(_team.Id);
            var catalog = await _sponsorRepository.GetAllAsync();

            string Describe(SponsorType slot)
            {
                var deal = current.FirstOrDefault(d => d.SponsorType == slot);
                if (deal is null)
                    return "Kein Sponsor.";
                var sponsor = catalog.FirstOrDefault(s => s.Id == deal.SponsorId);
                if (sponsor is null)
                    return "Kein Sponsor.";
                int expiresAfterSeason = deal.StartSeason + deal.Duration - 1;
                return $"{sponsor.Name} ({sponsor.SeasonPayment:N0} € / Saison) - läuft bis Saison {expiresAfterSeason}";
            }

            CurrentMainText = Describe(SponsorType.Main);
            CurrentPerimeterText = Describe(SponsorType.Perimeter);
            CurrentKitText = Describe(SponsorType.Kit);
        }

        [RelayCommand]
        private async Task Sign(Sponsor sponsor)
        {
            if (_team is null || _session.State is null)
                return;

            try
            {
                var signed = await _sponsorService.SignAsync(_team, sponsor, _session.State.Season);
                if (signed is null)
                {
                    StatusText = "Der aktuelle Vertrag in diesem Bereich läuft noch - erst nach Ablauf neu verhandelbar.";
                    return;
                }

                await RefreshAsync();
                StatusText = $"Deal mit {sponsor.Name} unterschrieben.";
            }
            catch (Exception ex)
            {
                Log.Error("Sponsor deal failed.", ex);
                StatusText = "Deal konnte nicht abgeschlossen werden.";
            }
        }
    }
}
