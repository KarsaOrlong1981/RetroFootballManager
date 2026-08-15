using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Common;
using RetroFootballManager.Core.Models;
using RetroFootballManager.Data;
using RetroFootballManager.Logging;
using RetroFootballManager.Models;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public record ActiveScoutingRow(int PlayerId, string PlayerName, string TeamName, int DaysRemaining);
    public record ScoutRecommendationRow(int PlayerId, string PlayerName, string TeamName, string PositionShort, string Reason);
    public record ScoutedPlayerRow(int PlayerId, string PlayerName, string TeamName, string ScoutedDateText, string PositionShort);
    public record ScoutRow(int EmployeeId, string Name, int ScoutingAbility, int ActiveAssignments, string FocusSummary);

    public partial class ScoutingViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<ScoutingViewModel>();

        private readonly GameSession _session;
        private readonly SaveGameService _saveGame;
        private readonly INavigationService _navigation;
        private readonly TransferMarketService _market;

        private int _selectedProfilePlayerId;
        private Player? _selectedProfilePlayer;

        public ScoutingViewModel(
            IDispatcher dispatcher, GameSession session, SaveGameService saveGame, INavigationService navigation,
            TransferMarketService market)
            : base(dispatcher)
        {
            _session = session;
            _saveGame = saveGame;
            _navigation = navigation;
            _market = market;
            Title = "Scouting";
        }

        public ObservableCollection<ActiveScoutingRow> ActiveScouting { get; } = [];
        public ObservableCollection<ScoutRecommendationRow> Recommendations { get; } = [];
        public ObservableCollection<ScoutedPlayerRow> ScoutedPlayers { get; } = [];
        public ObservableCollection<ScoutRow> Scouts { get; } = [];

        [ObservableProperty] private bool _hasScout;
        [ObservableProperty] private string _statusText = string.Empty;

        // Scouting-Fokus dialog
        [ObservableProperty] private bool _isScoutingFocusDialogOpen;
        [ObservableProperty] private int _focusScoutEmployeeId;
        [ObservableProperty] private string _focusScoutName = string.Empty;
        [ObservableProperty] private PositionFilterOption _focusPosition = PositionFilterOption.All[0];
        [ObservableProperty] private string _focusMinAgeText = string.Empty;
        [ObservableProperty] private string _focusMaxAgeText = string.Empty;
        [ObservableProperty] private string _focusMinTalentText = string.Empty;
        [ObservableProperty] private string _focusMinRatingText = string.Empty;
        [ObservableProperty] private CharacterFilterOption _focusCharacter = CharacterFilterOption.All[0];
        [ObservableProperty] private PersonalityFilterOption _focusPersonality = PersonalityFilterOption.All[0];
        [ObservableProperty] private NationalityFilterOption _focusNationality = NationalityFilterOption.All[0];
        [ObservableProperty] private AttributeFilterOption _focusAttribute = AttributeFilterOption.All[0];
        [ObservableProperty] private string _focusAttributeMinValueText = string.Empty;
        [ObservableProperty] private string _focusStatusText = string.Empty;

        public IReadOnlyList<PositionFilterOption> PositionFilterOptions => PositionFilterOption.All;
        public IReadOnlyList<CharacterFilterOption> CharacterFilterOptions => CharacterFilterOption.All;
        public IReadOnlyList<PersonalityFilterOption> PersonalityFilterOptions => PersonalityFilterOption.All;
        public IReadOnlyList<NationalityFilterOption> NationalityFilterOptions => NationalityFilterOption.All;
        public IReadOnlyList<AttributeFilterOption> AttributeFilterOptions => AttributeFilterOption.All;

        [ObservableProperty] private bool _isPlayerProfileOpen;
        [ObservableProperty] private PlayerProfile? _selectedProfile;
        [ObservableProperty] private string _selectedPlayerName = string.Empty;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSelectedPlayerNotScouted))]
        private bool _isSelectedPlayerScouted;
        [ObservableProperty] private bool _isBeingScouted;
        [ObservableProperty] private string _scoutingStatusText = string.Empty;
        [ObservableProperty] private bool _canOfferForSelectedPlayer;
        [ObservableProperty] private double _selectedPlayerMarketValue;
        [ObservableProperty] private string _offerStatusText = string.Empty;

        public bool IsSelectedPlayerNotScouted => !IsSelectedPlayerScouted;

        public async Task InitializeAsync()
        {
            ActiveScouting.Clear();
            Recommendations.Clear();
            ScoutedPlayers.Clear();
            Scouts.Clear();

            var team = _session.ManagerTeam;
            var state = _session.State;
            if (team is null || state is null)
                return;

            HasScout = ScoutingService.HasScout(team);
            if (!HasScout)
                return;

            try
            {
                var names = _session.Teams.ToDictionary(t => t.Id, t => t.Name);
                var assignments = await _saveGame.GetActiveScoutingAsync(team.Id);
                var foci = await _saveGame.GetScoutingFocusesAsync(team.Id);

                foreach (var scout in team.Employees.Where(e => e.EmployeeType == EmployeeType.Scout))
                {
                    int active = assignments.Count(a => a.ScoutEmployeeId == scout.Id);
                    var focus = foci.FirstOrDefault(f => f.ScoutEmployeeId == scout.Id);
                    string summary = focus is null
                        ? "Kein Fokus (Team-Schwächen)"
                        : DescribeFocus(focus);
                    Scouts.Add(new ScoutRow(scout.Id, scout.Name, scout.ScoutingAbility, active, summary));
                }

                foreach (var assignment in assignments)
                {
                    var player = _session.Teams.SelectMany(t => t.Players).FirstOrDefault(p => p.Id == assignment.PlayerId);
                    if (player is null)
                        continue;
                    int daysLeft = Math.Max(0, (assignment.CompletionDate.Date - state.CurrentDate.Date).Days);
                    ActiveScouting.Add(new ActiveScoutingRow(
                        player.Id, player.Name, names.GetValueOrDefault(player.TeamId, "?"), daysLeft));
                }

                var recommendations = ScoutingService.GetRecommendations(team, _session.Teams, state.Season, state.CurrentDate.Month);
                foreach (var rec in recommendations)
                    Recommendations.Add(new ScoutRecommendationRow(
                        rec.PlayerId, rec.PlayerName, rec.TeamName, PositionDisplay.Short(rec.Position), rec.Reason));

                var scouted = await _saveGame.GetScoutedPlayersAsync(team.Id);
                foreach (var row in scouted.OrderByDescending(s => s.ScoutedDate))
                {
                    var player = _session.Teams.SelectMany(t => t.Players).FirstOrDefault(p => p.Id == row.PlayerId);
                    if (player is null)
                        continue;
                    ScoutedPlayers.Add(new ScoutedPlayerRow(
                        player.Id, player.Name, names.GetValueOrDefault(player.TeamId, "?"), row.ScoutedDate.ToString("dd.MM.yyyy"), player.ShortPositionName));
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to load scouting overview.", ex);
                StatusText = "Daten konnten nicht geladen werden.";
            }
        }

        [RelayCommand]
        private async Task ShowProfile(int playerId)
        {
            var team = _session.ManagerTeam;
            var state = _session.State;
            var player = _session.Teams.SelectMany(t => t.Players).FirstOrDefault(p => p.Id == playerId);
            if (player is null)
                return;

            _selectedProfilePlayerId = playerId;
            _selectedProfilePlayer = player;
            SelectedPlayerName = player.Name;
            IsSelectedPlayerScouted = player.IsScouted;
            ScoutingStatusText = string.Empty;
            OfferStatusText = string.Empty;
            CanOfferForSelectedPlayer = false;

            var activeScouting = team is null ? null : await _saveGame.GetActiveScoutingForPlayerAsync(team.Id, playerId);
            IsBeingScouted = activeScouting is not null;
            if (activeScouting is not null && state is not null)
            {
                int daysLeft = Math.Max(0, (activeScouting.CompletionDate.Date - state.CurrentDate.Date).Days);
                ScoutingStatusText = $"Wird gescoutet (noch {daysLeft} Tage).";
            }

            if (!player.IsScouted)
            {
                SelectedProfile = null;
            }
            else
            {
                var contract = state is null ? null : await _saveGame.GetActivePlayerContractAsync(player.Id, state.CurrentDate);
                var listing = await _saveGame.GetTransferListingForPlayerAsync(player.Id);
                var seasonStats = state is null ? null : await _saveGame.GetPlayerSeasonStatsAsync(player.Id, state.Season);
                var careerStats = await _saveGame.GetPlayerCareerStatsAsync(player.Id);
                var competitionStats = await _saveGame.GetPlayerCompetitionBreakdownAsync(player.Id);
                SelectedProfile = PlayerProfile.From(player, contract, listing, seasonStats, careerStats, competitionStats);

                // Never offer for our own players - only for scouted players at other clubs, and
                // only while no offer/listing already exists for them.
                SelectedPlayerMarketValue = contract?.MarketValue ?? TransferAiService.EstimateMarketValue(player);
                CanOfferForSelectedPlayer = team is not null && player.TeamId != team.Id && listing is null;
            }
            IsPlayerProfileOpen = true;
        }

        [RelayCommand]
        private async Task ScoutPlayer()
        {
            var team = _session.ManagerTeam;
            var state = _session.State;
            var player = _session.Teams.SelectMany(t => t.Players).FirstOrDefault(p => p.Id == _selectedProfilePlayerId);
            if (team is null || player is null || state is null)
                return;

            var (started, error) = await _saveGame.TryStartScoutingAsync(team, player, state.CurrentDate);
            if (started)
            {
                IsBeingScouted = true;
                ScoutingStatusText = "Scouting gestartet - Ergebnis in 14 Tagen.";
                await InitializeAsync();
            }
            else
            {
                ScoutingStatusText = error ?? "Scouting konnte nicht gestartet werden.";
            }
        }

        [RelayCommand]
        private void CloseProfile() => IsPlayerProfileOpen = false;

        [RelayCommand]
        private Task MakeUnsolicitedTransferOffer() => MakeUnsolicitedOffer(isLoan: false);

        [RelayCommand]
        private Task MakeUnsolicitedLoanOffer() => MakeUnsolicitedOffer(isLoan: true);

        // Unsolicited offer for a scouted-only player their club never listed - a higher price is
        // needed since the club wasn't looking to sell (see TransferAiService.ShouldAcceptOffer).
        // The seller's COM manager decides on their next weekly tick, not immediately.
        private async Task MakeUnsolicitedOffer(bool isLoan)
        {
            if (IsBusy || !CanOfferForSelectedPlayer) return;
            var team = _session.ManagerTeam;
            var state = _session.State;
            var player = _selectedProfilePlayer;
            var sellingTeam = player is null ? null : _session.Teams.FirstOrDefault(t => t.Id == player.TeamId);
            if (team is null || state is null || player is null || sellingTeam is null)
                return;

            if (!TransferMarketService.CanBuy(team, out string? balanceError))
            {
                OfferStatusText = balanceError!;
                return;
            }

            IsBusy = true;
            OfferStatusText = "Angebot wird abgegeben …";
            try
            {
                // Starting offer at fair market value - the seller wasn't looking to sell, so this
                // will often come back as a (higher) counter-offer rather than an instant accept;
                // see TransferMarketPage's "Eigene abgegebene Angebote" to respond to it.
                double fee = SelectedPlayerMarketValue;
                double wage = SelectedPlayerMarketValue * 0.15;
                await _market.MakeUnsolicitedOfferAsync(
                    player, sellingTeam, team, fee, wage, state.Season, state.CurrentDate, isLoan);
                OfferStatusText = $"Angebot für {player.Name} abgegeben - {sellingTeam.Name} entscheidet in Kürze (siehe Transfermarkt).";
                CanOfferForSelectedPlayer = false;
            }
            catch (Exception ex)
            {
                Log.Error("Could not submit unsolicited offer.", ex);
                OfferStatusText = "Angebot fehlgeschlagen.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RemoveScoutedPlayer(int playerId)
        {
            var team = _session.ManagerTeam;
            if (team is null)
                return;

            try
            {
                await _saveGame.RemoveScoutedPlayerAsync(team.Id, playerId);
                var row = ScoutedPlayers.FirstOrDefault(r => r.PlayerId == playerId);
                if (row is not null)
                    ScoutedPlayers.Remove(row);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to remove scouted player.", ex);
            }
        }

        [RelayCommand]
        private void OpenScoutingFocus(int employeeId)
        {
            var scout = _session.ManagerTeam?.Employees.FirstOrDefault(e => e.Id == employeeId);
            if (scout is null)
                return;

            FocusScoutEmployeeId = employeeId;
            FocusScoutName = scout.Name;
            FocusPosition = PositionFilterOption.All[0];
            FocusMinAgeText = string.Empty;
            FocusMaxAgeText = string.Empty;
            FocusMinTalentText = string.Empty;
            FocusMinRatingText = string.Empty;
            FocusCharacter = CharacterFilterOption.All[0];
            FocusPersonality = PersonalityFilterOption.All[0];
            FocusNationality = NationalityFilterOption.All[0];
            FocusAttribute = AttributeFilterOption.All[0];
            FocusAttributeMinValueText = string.Empty;
            FocusStatusText = string.Empty;
            IsScoutingFocusDialogOpen = true;
        }

        [RelayCommand]
        private void CloseScoutingFocus() => IsScoutingFocusDialogOpen = false;

        [RelayCommand]
        private async Task SaveScoutingFocus()
        {
            var team = _session.ManagerTeam;
            var state = _session.State;
            var scout = team?.Employees.FirstOrDefault(e => e.Id == FocusScoutEmployeeId);
            if (team is null || state is null || scout is null)
                return;

            var focus = new ScoutingFocus
            {
                Position = FocusPosition.Value,
                MinAge = ParseIntOrNull(FocusMinAgeText),
                MaxAge = ParseIntOrNull(FocusMaxAgeText),
                MinTalent = ParseIntOrNull(FocusMinTalentText),
                MinRating = ParseIntOrNull(FocusMinRatingText),
                CharacterType = FocusCharacter.Value,
                PersonalityType = FocusPersonality.Value,
                Nationality = FocusNationality.Value,
            };
            if (FocusAttribute.Value is { } attribute && ParseIntOrNull(FocusAttributeMinValueText) is { } minValue)
                focus.AttributeFilters = [new AttributeFilter(attribute, minValue)];

            var (success, error) = await _saveGame.TryAssignScoutingFocusAsync(team, scout, focus, state.CurrentDate);
            if (success)
            {
                IsScoutingFocusDialogOpen = false;
                await InitializeAsync();
            }
            else
            {
                FocusStatusText = error ?? "Fokus konnte nicht zugewiesen werden.";
            }
        }

        private static int? ParseIntOrNull(string text) => int.TryParse(text, out int value) ? value : null;

        private static string DescribeFocus(ScoutingFocus focus)
        {
            if (!focus.HasAnyFilter)
                return "Kein Fokus (Team-Schwächen)";

            var parts = new List<string>();
            if (focus.Position is { } position) parts.Add(PositionDisplay.Short(position));
            if (focus.MinAge is { } minAge) parts.Add($"Alter ≥{minAge}");
            if (focus.MaxAge is { } maxAge) parts.Add($"Alter ≤{maxAge}");
            if (focus.MinTalent is { } minTalent) parts.Add($"Talent ≥{minTalent}");
            if (focus.MinRating is { } minRating) parts.Add($"Rating ≥{minRating}");
            if (focus.CharacterType is { } characterType) parts.Add(InMatchCharacterDisplay.Name(characterType));
            if (focus.PersonalityType is { } personalityType) parts.Add(PersonalityDisplay.Name(personalityType));
            if (focus.Nationality is { } nationality) parts.Add(nationality.ToString());
            foreach (var attributeFilter in focus.AttributeFilters)
                parts.Add($"{attributeFilter.Attribute} ≥{attributeFilter.MinValue}");

            return string.Join(", ", parts);
        }

        [RelayCommand]
        private Task Back() => _navigation.GoBackAsync();
    }
}
