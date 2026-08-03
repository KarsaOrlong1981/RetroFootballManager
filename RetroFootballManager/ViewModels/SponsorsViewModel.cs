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

        // While a slot has an active contract, its offer list/sign button is hidden and
        // the signed sponsor's full details are shown instead - until the contract expires.
        [ObservableProperty] private bool _isMainSlotSigned;
        [ObservableProperty] private Sponsor? _signedMainSponsor;
        [ObservableProperty] private string _mainSignedUntilText = string.Empty;

        [ObservableProperty] private bool _isPerimeterSlotSigned;
        [ObservableProperty] private Sponsor? _signedPerimeterSponsor;
        [ObservableProperty] private string _perimeterSignedUntilText = string.Empty;

        [ObservableProperty] private bool _isKitSlotSigned;
        [ObservableProperty] private Sponsor? _signedKitSponsor;
        [ObservableProperty] private string _kitSignedUntilText = string.Empty;

        public bool IsMainSlotOpen => !IsMainSlotSigned;
        public bool IsPerimeterSlotOpen => !IsPerimeterSlotSigned;
        public bool IsKitSlotOpen => !IsKitSlotSigned;
        public bool ShowKitSignedCard => KitSlotAvailable && IsKitSlotSigned;
        public bool ShowKitOffers => KitSlotAvailable && IsKitSlotOpen;

        partial void OnIsMainSlotSignedChanged(bool value) => OnPropertyChanged(nameof(IsMainSlotOpen));
        partial void OnIsPerimeterSlotSignedChanged(bool value) => OnPropertyChanged(nameof(IsPerimeterSlotOpen));

        partial void OnIsKitSlotSignedChanged(bool value)
        {
            OnPropertyChanged(nameof(IsKitSlotOpen));
            OnPropertyChanged(nameof(ShowKitSignedCard));
            OnPropertyChanged(nameof(ShowKitOffers));
        }

        partial void OnKitSlotAvailableChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowKitSignedCard));
            OnPropertyChanged(nameof(ShowKitOffers));
        }

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

            var current = await _sponsorshipRepository.GetByTeamAsync(_team.Id);
            var catalog = await _sponsorRepository.GetAllAsync();
            var currentSeason = _session.State?.Season ?? 0;

            (Sponsor Sponsor, string Text)? ActiveDeal(SponsorType slot)
            {
                var deal = current.FirstOrDefault(d => d.SponsorType == slot);
                if (deal is null || currentSeason >= deal.StartSeason + deal.Duration)
                    return null;
                var sponsor = catalog.FirstOrDefault(s => s.Id == deal.SponsorId);
                if (sponsor is null)
                    return null;
                int expiresAfterSeason = deal.StartSeason + deal.Duration - 1;
                return (sponsor, $"Läuft bis Saison {expiresAfterSeason}");
            }

            var mainDeal = ActiveDeal(SponsorType.Main);
            IsMainSlotSigned = mainDeal is not null;
            SignedMainSponsor = mainDeal?.Sponsor;
            MainSignedUntilText = mainDeal?.Text ?? string.Empty;
            CurrentMainText = mainDeal is null ? "Kein Sponsor." : $"{mainDeal.Value.Sponsor.Name} ({mainDeal.Value.Sponsor.SeasonPayment:N0} € / Saison)";

            var perimeterDeal = ActiveDeal(SponsorType.Perimeter);
            IsPerimeterSlotSigned = perimeterDeal is not null;
            SignedPerimeterSponsor = perimeterDeal?.Sponsor;
            PerimeterSignedUntilText = perimeterDeal?.Text ?? string.Empty;
            CurrentPerimeterText = perimeterDeal is null ? "Kein Sponsor." : $"{perimeterDeal.Value.Sponsor.Name} ({perimeterDeal.Value.Sponsor.SeasonPayment:N0} € / Saison)";

            var kitDeal = ActiveDeal(SponsorType.Kit);
            IsKitSlotSigned = kitDeal is not null;
            SignedKitSponsor = kitDeal?.Sponsor;
            KitSignedUntilText = kitDeal?.Text ?? string.Empty;
            CurrentKitText = kitDeal is null ? "Kein Sponsor." : $"{kitDeal.Value.Sponsor.Name} ({kitDeal.Value.Sponsor.SeasonPayment:N0} € / Saison)";

            MainOffers.Clear();
            if (!IsMainSlotSigned)
            {
                foreach (var s in await _sponsorService.GetAvailableOffersAsync(_team, SponsorType.Main))
                    MainOffers.Add(s);
            }

            PerimeterOffers.Clear();
            if (!IsPerimeterSlotSigned)
            {
                foreach (var s in await _sponsorService.GetAvailableOffersAsync(_team, SponsorType.Perimeter))
                    PerimeterOffers.Add(s);
            }

            KitOffers.Clear();
            if (!IsKitSlotSigned)
            {
                foreach (var s in await _sponsorService.GetAvailableOffersAsync(_team, SponsorType.Kit))
                    KitOffers.Add(s);
            }
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
