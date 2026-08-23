using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Common;
using RetroFootballManager.Core.Models;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Logging;
using RetroFootballManager.Models;
using RetroFootballManager.Services;
using System.Collections.ObjectModel;

namespace RetroFootballManager.ViewModels
{
    public record OwnPlayerRow(
        int PlayerId, string PlayerName, string Position, double Rating, double AnnualSalary, double MarketValue,
        bool IsListed, bool IsLoanListed)
    {
        public bool CanOffer => !IsListed;
        public bool IsTransferListed => IsListed && !IsLoanListed;
    }

    public record MarketListingRow(
        int ListingId, int PlayerId, string PlayerName, string Position, double Rating, string TeamName,
        double AskingPrice, bool IsLoan);

    public record OwnOfferRow(int OfferId, int ListingId, string OfferingTeamName, double Fee, double Wage);

    public record OwnListingRow(
        int ListingId, int PlayerId, string PlayerName, double AskingPrice, bool IsLoan, List<OwnOfferRow> Offers)
    {
        public bool HasOffers => Offers.Count > 0;
    }

    public record OutgoingOfferRow(
        int OfferId, string PlayerName, string SellingTeamName, double Fee, double Wage, bool IsLoan, bool IsCountered,
        double CounterFee);

    public partial class TransferMarketViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<TransferMarketViewModel>();

        private const int MinimumForeignListings = 30;

        private readonly GameSession _session;
        private readonly SaveGameService _saveGame;
        private readonly TransferMarketService _market;
        private readonly TransferListingRepository _listingRepo;
        private readonly TransferOfferRepository _offerRepo;
        private readonly ContractBonusRepository _bonusRepo;

        private Team? _team;
        private Dictionary<int, TransferListing> _listingsById = new();
        private Dictionary<int, TransferOffer> _offersById = new();
        private readonly Random _rng = new();

        // Shared negotiation dialog (manager phase + player phase) - see
        // NegotiationDialogViewModel. Bound in TransferMarketPage.xaml via
        // BindingContext="{Binding Negotiation}".
        public NegotiationDialogViewModel Negotiation { get; }

        public TransferMarketViewModel(
            IDispatcher dispatcher, GameSession session, SaveGameService saveGame, TransferMarketService market,
            TransferListingRepository listingRepo, TransferOfferRepository offerRepo, ContractBonusRepository bonusRepo,
            NegotiationDialogViewModel negotiation)
            : base(dispatcher)
        {
            _session = session;
            _saveGame = saveGame;
            _market = market;
            _listingRepo = listingRepo;
            _offerRepo = offerRepo;
            _bonusRepo = bonusRepo;
            Negotiation = negotiation;
            Title = "Transfermarkt";
        }

        [ObservableProperty] private string _statusText = string.Empty;

        public ObservableCollection<OwnPlayerRow> OwnPlayers { get; } = [];
        public ObservableCollection<MarketListingRow> MarketListings { get; } = [];
        public ObservableCollection<OwnListingRow> OwnListings { get; } = [];
        public ObservableCollection<OutgoingOfferRow> OutgoingOffers { get; } = [];

        [ObservableProperty] private bool _isPlayerProfileOpen;
        [ObservableProperty] private PlayerProfile? _selectedProfile;
        [ObservableProperty] private string _selectedPlayerName = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSelectedPlayerNotScouted))]
        private bool _isSelectedPlayerScouted;

        [ObservableProperty] private bool _isBeingScouted;
        [ObservableProperty] private string _scoutingStatusText = string.Empty;

        public bool IsSelectedPlayerNotScouted => !IsSelectedPlayerScouted;

        public async Task InitializeAsync()
        {
            _team = _session.ManagerTeam;
            if (_team is null || _session.State is null)
                return;

            try
            {
                await _market.EnsureMinimumForeignListingsAsync(
                    _session.Teams, _session.State.Season, _session.State.CurrentDate, MinimumForeignListings, _rng);
            }
            catch (Exception ex)
            {
                Log.Error("Market replenishment failed.", ex);
            }

            await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            if (_team is null || _session.State is null)
                return;

            int season = _session.State.Season;
            var allListings = await _listingRepo.GetBySeasonAsync(season);
            _listingsById = allListings.ToDictionary(l => l.Id);
            var teamsById = _session.Teams.ToDictionary(t => t.Id);
            var teamNames = _session.Teams.ToDictionary(t => t.Id, t => t.Name);
            var ownListingByPlayerId = allListings.Where(l => l.TeamId == _team.Id).ToDictionary(l => l.PlayerId);

            OwnPlayers.Clear();
            foreach (var player in _team.Players.OrderBy(p => p.Position).ThenByDescending(p => p.Rating))
            {
                var contract = await _saveGame.GetActivePlayerContractAsync(player.Id, _session.State.CurrentDate);
                double salary = contract?.AnnualSalary ?? 0;
                double marketValue = contract?.MarketValue ?? TransferAiService.EstimateMarketValue(player);
                bool isListed = ownListingByPlayerId.TryGetValue(player.Id, out var ownListing);

                OwnPlayers.Add(new OwnPlayerRow(
                    player.Id, player.Name, PositionDisplay.Short(player.Position), Math.Round(player.Rating, 1),
                    salary, marketValue, isListed, isListed && ownListing!.IsLoanListing));
            }

            MarketListings.Clear();
            foreach (var listing in allListings.Where(l => l.TeamId != _team.Id && !l.IsUnsolicited))
            {
                if (!teamsById.TryGetValue(listing.TeamId, out var sellerTeam))
                    continue;
                var player = sellerTeam.Players.FirstOrDefault(p => p.Id == listing.PlayerId);
                if (player is null)
                    continue;

                MarketListings.Add(new MarketListingRow(
                    listing.Id, player.Id, player.Name, PositionDisplay.Short(player.Position), Math.Round(player.Rating, 1),
                    sellerTeam.Name, listing.AskingPrice, listing.IsLoanListing));
            }

            OwnListings.Clear();
            _offersById.Clear();
            foreach (var listing in allListings.Where(l => l.TeamId == _team.Id))
            {
                var player = _team.Players.FirstOrDefault(p => p.Id == listing.PlayerId);
                var offersForListing = await _offerRepo.GetByListingAsync(listing.Id);
                var pendingRows = new List<OwnOfferRow>();
                foreach (var offer in offersForListing.Where(o => o.Status == TransferOfferStatus.Pending))
                {
                    _offersById[offer.Id] = offer;
                    pendingRows.Add(new OwnOfferRow(
                        offer.Id, listing.Id, teamNames.GetValueOrDefault(offer.OfferingTeamId, "?"),
                        offer.OfferedFee, offer.WageOffer));
                }

                OwnListings.Add(new OwnListingRow(
                    listing.Id, listing.PlayerId, player?.Name ?? "?", listing.AskingPrice, listing.IsLoanListing, pendingRows));
            }

            OutgoingOffers.Clear();
            var ownOutgoingOffers = await _offerRepo.GetPendingByTeamAsync(_team.Id);
            foreach (var offer in ownOutgoingOffers)
            {
                if (!_listingsById.TryGetValue(offer.ListingId, out var listing)
                    || !teamsById.TryGetValue(listing.TeamId, out var sellerTeam))
                    continue;
                var player = sellerTeam.Players.FirstOrDefault(p => p.Id == listing.PlayerId);

                _offersById[offer.Id] = offer;
                bool isCountered = offer.Status == TransferOfferStatus.Countered;
                OutgoingOffers.Add(new OutgoingOfferRow(
                    offer.Id, player?.Name ?? "?", sellerTeam.Name, offer.OfferedFee, offer.WageOffer, listing.IsLoanListing,
                    isCountered, offer.CounterFee));
            }
        }

        private int _selectedProfilePlayerId;

        [RelayCommand]
        private async Task ShowProfile(int playerId)
        {
            if (_session.State is null)
                return;

            var player = _session.Teams.SelectMany(t => t.Players).FirstOrDefault(p => p.Id == playerId);
            if (player is null)
                return;

            _selectedProfilePlayerId = playerId;
            SelectedPlayerName = player.Name;
            ScoutingStatusText = string.Empty;

            var listing = await _saveGame.GetTransferListingForPlayerAsync(player.Id);
            // A player publicly offered for transfer/loan reveals their full profile regardless of
            // scouting status - COM teams can snap them up during the 14-day scouting wait otherwise.
            bool isPubliclyListed = listing is not null && !listing.IsUnsolicited;
            IsSelectedPlayerScouted = player.IsScouted || isPubliclyListed;

            var activeScouting = _team is null ? null : await _saveGame.GetActiveScoutingForPlayerAsync(_team.Id, playerId);
            IsBeingScouted = activeScouting is not null;
            if (activeScouting is not null)
            {
                int daysLeft = Math.Max(0, (activeScouting.CompletionDate.Date - _session.State.CurrentDate.Date).Days);
                ScoutingStatusText = $"Wird gescoutet (noch {daysLeft} Tage).";
            }

            if (!IsSelectedPlayerScouted)
            {
                SelectedProfile = null;
            }
            else
            {
                var contract = await _saveGame.GetActivePlayerContractAsync(player.Id, _session.State.CurrentDate);
                var seasonStats = await _saveGame.GetPlayerSeasonStatsAsync(player.Id, _session.State.Season);
                var careerStats = await _saveGame.GetPlayerCareerStatsAsync(player.Id);
                var competitionStats = await _saveGame.GetPlayerCompetitionBreakdownAsync(player.Id);
                var bonuses = contract is not null ? await _bonusRepo.GetByContractAsync(contract.Id) : null;
                SelectedProfile = PlayerProfile.From(player, contract, listing, seasonStats, careerStats, competitionStats, bonuses);
            }
            IsPlayerProfileOpen = true;
        }

        [RelayCommand]
        private async Task ScoutPlayer()
        {
            var player = _session.Teams.SelectMany(t => t.Players).FirstOrDefault(p => p.Id == _selectedProfilePlayerId);
            if (_team is null || player is null || _session.State is null)
                return;

            var (started, error) = await _saveGame.TryStartScoutingAsync(_team, player, _session.State.CurrentDate);
            if (started)
            {
                IsBeingScouted = true;
                ScoutingStatusText = "Scouting gestartet - Ergebnis in 14 Tagen.";
            }
            else
            {
                ScoutingStatusText = error ?? "Scouting konnte nicht gestartet werden.";
            }
        }

        [RelayCommand]
        private void CloseProfile() => IsPlayerProfileOpen = false;

        [RelayCommand]
        private async Task OfferForTransfer(OwnPlayerRow row)
        {
            if (IsBusy) return;
            if (_team is null || _session.State is null)
                return;

            var player = _team.Players.FirstOrDefault(p => p.Id == row.PlayerId);
            if (player is null)
                return;

            IsBusy = true;
            StatusText = $"{player.Name} wird zum Transfer angeboten …";
            try
            {
                await _market.ListPlayerAsync(
                    player, _team, row.MarketValue, _session.State.Season, _session.State.CurrentDate, isLoanListing: false);
                StatusText = $"{player.Name} zum Transfer angeboten.";
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                Log.Error("Could not list player for transfer.", ex);
                StatusText = "Angebot fehlgeschlagen.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task OfferForLoan(OwnPlayerRow row)
        {
            if (IsBusy) return;
            if (_team is null || _session.State is null)
                return;

            var player = _team.Players.FirstOrDefault(p => p.Id == row.PlayerId);
            if (player is null)
                return;

            IsBusy = true;
            StatusText = $"{player.Name} wird zur Leihe angeboten …";
            try
            {
                await _market.ListPlayerAsync(
                    player, _team, row.MarketValue, _session.State.Season, _session.State.CurrentDate, isLoanListing: true);
                StatusText = $"{player.Name} zur Leihe angeboten.";
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                Log.Error("Could not list player for loan.", ex);
                StatusText = "Angebot fehlgeschlagen.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task WithdrawListing(OwnPlayerRow row)
        {
            if (IsBusy) return;
            if (!row.IsListed)
                return;
            var listing = _listingsById.Values.FirstOrDefault(l => l.PlayerId == row.PlayerId);
            if (listing is null)
                return;

            IsBusy = true;
            StatusText = $"Angebot für {row.PlayerName} wird zurückgezogen …";
            try
            {
                await _market.RemoveListingAsync(listing);
                StatusText = $"Angebot für {row.PlayerName} zurückgezogen.";
                await RefreshAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task MakeOffer(MarketListingRow row)
        {
            if (IsBusy) return;
            if (_team is null || _session.State is null || !_listingsById.TryGetValue(row.ListingId, out var listing))
                return;

            var player = _session.Teams.SelectMany(t => t.Players).FirstOrDefault(p => p.Id == row.PlayerId);
            var sellingTeam = _session.Teams.FirstOrDefault(t => t.Id == listing.TeamId);
            if (player is null || sellingTeam is null)
                return;

            var seasonStats = await _saveGame.GetPlayerSeasonStatsAsync(player.Id, _session.State.Season);
            string? error = await Negotiation.TryStartBuyOrLoanNegotiationAsync(
                _team, listing, player, sellingTeam, seasonStats, _session.State.Season, _session.State.CurrentDate, RefreshAsync);
            if (error is not null)
                StatusText = error;
        }

        [RelayCommand]
        private async Task NegotiateOwnOffer(OwnOfferRow row)
        {
            if (IsBusy) return;
            if (_team is null || _session.State is null
                || !_offersById.TryGetValue(row.OfferId, out var offer)
                || !_listingsById.TryGetValue(row.ListingId, out var listing))
                return;

            var buyingTeam = _session.Teams.FirstOrDefault(t => t.Id == offer.OfferingTeamId);
            var player = _team.Players.FirstOrDefault(p => p.Id == listing.PlayerId);
            if (buyingTeam is null || player is null)
                return;

            var seasonStats = await _saveGame.GetPlayerSeasonStatsAsync(player.Id, _session.State.Season);
            await Negotiation.TryStartSellNegotiationAsync(
                _team, offer, listing, player, buyingTeam, seasonStats, _session.State.CurrentDate, RefreshAsync);
        }

        [RelayCommand]
        private async Task RenewContract(OwnPlayerRow row)
        {
            if (IsBusy) return;
            if (_team is null || _session.State is null)
                return;

            var player = _team.Players.FirstOrDefault(p => p.Id == row.PlayerId);
            if (player is null)
                return;

            string? error = await Negotiation.TryStartRenewalNegotiationAsync(
                _team, player, _session.State.Season, _session.State.CurrentDate, RefreshAsync);
            if (error is not null)
                StatusText = error;
        }

        [RelayCommand]
        private async Task AcceptOffer(OwnOfferRow row)
        {
            if (IsBusy) return;
            if (_team is null || _session.State is null
                || !_offersById.TryGetValue(row.OfferId, out var offer)
                || !_listingsById.TryGetValue(row.ListingId, out var listing))
                return;

            var buyingTeam = _session.Teams.FirstOrDefault(t => t.Id == offer.OfferingTeamId);
            var player = _team.Players.FirstOrDefault(p => p.Id == listing.PlayerId);
            if (buyingTeam is null || player is null)
                return;

            IsBusy = true;
            StatusText = "Transfer wird abgewickelt …";
            try
            {
                if (listing.IsLoanListing)
                {
                    await _market.LoanOutAsync(
                        player, _team, buyingTeam, _session.State.CurrentDate, _session.State.CurrentDate.AddMonths(6),
                        offer.WageOffer);
                    await _market.RemoveListingAsync(listing);
                    StatusText = $"{player.Name} an {buyingTeam.Name} verliehen.";
                }
                else
                {
                    await _market.AcceptOfferAsync(offer, listing, _team, buyingTeam, player, _session.State.CurrentDate);
                    StatusText = $"{player.Name} an {buyingTeam.Name} verkauft.";
                }
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                Log.Error("Could not accept offer.", ex);
                StatusText = "Transfer fehlgeschlagen.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task AcceptCounterOffer(OutgoingOfferRow row)
        {
            if (IsBusy) return;
            if (_team is null || _session.State is null
                || !_offersById.TryGetValue(row.OfferId, out var offer)
                || !_listingsById.TryGetValue(offer.ListingId, out var listing))
                return;

            var sellingTeam = _session.Teams.FirstOrDefault(t => t.Id == listing.TeamId);
            var player = sellingTeam?.Players.FirstOrDefault(p => p.Id == listing.PlayerId);
            if (sellingTeam is null || player is null)
                return;

            if (!TransferMarketService.CanBuy(_team, out string? balanceError))
            {
                StatusText = balanceError!;
                return;
            }

            if (!listing.IsLoanListing && !TransferMarketService.CanAffordFee(_team, offer.CounterFee))
            {
                StatusText = $"Die geforderte Ablöse von {offer.CounterFee:N0} € würde den Kontostand zu weit ins Minus drücken.";
                return;
            }

            IsBusy = true;
            StatusText = "Gegenangebot wird angenommen …";
            try
            {
                await _market.AcceptCounterOfferAsync(offer, listing, sellingTeam, _team, player, _session.State.CurrentDate);
                StatusText = $"{player.Name} verpflichtet.";
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                Log.Error("Could not accept counter offer.", ex);
                StatusText = "Transfer fehlgeschlagen.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RejectOutgoingOffer(OutgoingOfferRow row)
        {
            if (IsBusy) return;
            if (!_offersById.TryGetValue(row.OfferId, out var offer))
                return;

            IsBusy = true;
            try
            {
                await _market.RejectOfferAsync(offer);
                StatusText = "Angebot zurückgezogen.";
                await RefreshAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RejectOffer(OwnOfferRow row)
        {
            if (IsBusy) return;
            if (!_offersById.TryGetValue(row.OfferId, out var offer))
                return;

            IsBusy = true;
            try
            {
                await _market.RejectOfferAsync(offer);
                StatusText = "Angebot abgelehnt.";
                await RefreshAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
