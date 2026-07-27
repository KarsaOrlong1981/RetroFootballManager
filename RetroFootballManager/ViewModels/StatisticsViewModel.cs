using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Logging;
using RetroFootballManager.Models;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public record StatCategoryOption(StatCategory Category, string Label);

    public record StatRankRow(int PlayerId, int Rank, string PlayerName, string TeamName, string PositionShort, double Value);

    public partial class StatisticsViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<StatisticsViewModel>();

        private readonly GameSession _session;
        private readonly PlayerStatsService _stats;
        private readonly SaveGameService _saveGame;
        private readonly INavigationService _navigation;

        public StatisticsViewModel(
            IDispatcher dispatcher, GameSession session, PlayerStatsService stats, SaveGameService saveGame,
            INavigationService navigation)
            : base(dispatcher)
        {
            _session = session;
            _stats = stats;
            _saveGame = saveGame;
            _navigation = navigation;
            Title = "Statistiken";
        }

        public ObservableCollection<StatCategoryOption> Categories { get; } =
        [
            new(StatCategory.TopScorers, "Torjäger"),
            new(StatCategory.TopAssists, "Assists"),
            new(StatCategory.ScorerPoints, "Scorerpunkte"),
            new(StatCategory.YellowCards, "Gelbe Karten"),
            new(StatCategory.RedCards, "Rote Karten"),
            new(StatCategory.FewestConceded, "Wenigste Gegentore"),
        ];

        public ObservableCollection<StatRankRow> Rows { get; } = [];

        [ObservableProperty] private StatCategoryOption? _selectedCategory;
        [ObservableProperty] private string _statusText = string.Empty;

        [ObservableProperty] private bool _isPlayerProfileOpen;
        [ObservableProperty] private PlayerProfile? _selectedProfile;
        [ObservableProperty] private string _selectedPlayerName = string.Empty;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSelectedPlayerNotScouted))]
        private bool _isSelectedPlayerScouted;

        [ObservableProperty] private bool _isBeingScouted;
        [ObservableProperty] private string _scoutingStatusText = string.Empty;

        public bool IsSelectedPlayerNotScouted => !IsSelectedPlayerScouted;

        public void Initialize()
        {
            if (SelectedCategory is null)
                SelectedCategory = Categories.FirstOrDefault();
            else
                _ = ReloadAsync();
        }

        partial void OnSelectedCategoryChanged(StatCategoryOption? value) => _ = ReloadAsync();

        private async Task ReloadAsync()
        {
            Rows.Clear();
            StatusText = string.Empty;

            var team = _session.ManagerTeam;
            var state = _session.State;
            if (team is null || state is null || SelectedCategory is null)
                return;

            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var top = await _stats.GetTopAsync(SelectedCategory.Category, state.Season, team.LeagueTier, state.MatchdayIndex);
                int rank = 1;
                foreach (var row in top)
                    Rows.Add(new StatRankRow(row.PlayerId, rank++, row.PlayerName, row.TeamName, row.PositionShort, row.Value));

                if (Rows.Count == 0)
                    StatusText = "Noch keine Daten für diese Rangliste.";
            }
            catch (Exception ex)
            {
                Log.Error("Failed to load statistics.", ex);
                StatusText = "Statistiken konnten nicht geladen werden.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private int _selectedProfilePlayerId;

        [RelayCommand]
        private async Task ShowProfile(int playerId)
        {
            var player = _session.Teams
                .SelectMany(t => t.Players.Concat(t.YouthPlayers))
                .FirstOrDefault(p => p.Id == playerId);
            if (player is null)
                return;

            _selectedProfilePlayerId = playerId;
            SelectedPlayerName = player.Name;
            IsSelectedPlayerScouted = player.IsScouted;
            ScoutingStatusText = string.Empty;

            var managerTeam = _session.ManagerTeam;
            var activeScouting = managerTeam is null
                ? null
                : await _saveGame.GetActiveScoutingForPlayerAsync(managerTeam.Id, playerId);
            IsBeingScouted = activeScouting is not null;
            if (activeScouting is not null && _session.State is not null)
            {
                int daysLeft = Math.Max(0, (activeScouting.CompletionDate.Date - _session.State.CurrentDate.Date).Days);
                ScoutingStatusText = $"Wird gescoutet (noch {daysLeft} Tage).";
            }

            if (!player.IsScouted)
            {
                SelectedProfile = null;
            }
            else
            {
                var contract = _session.State is null
                    ? null
                    : await _saveGame.GetActivePlayerContractAsync(player.Id, _session.State.CurrentDate);
                var listing = await _saveGame.GetTransferListingForPlayerAsync(player.Id);
                var seasonStats = _session.State is null
                    ? null
                    : await _saveGame.GetPlayerSeasonStatsAsync(player.Id, _session.State.Season);
                SelectedProfile = PlayerProfile.From(player, contract, listing, seasonStats);
            }
            IsPlayerProfileOpen = true;
        }

        [RelayCommand]
        private async Task ScoutPlayer()
        {
            var managerTeam = _session.ManagerTeam;
            var player = _session.Teams.SelectMany(t => t.Players).FirstOrDefault(p => p.Id == _selectedProfilePlayerId);
            if (managerTeam is null || player is null || _session.State is null)
                return;

            var (started, error) = await _saveGame.TryStartScoutingAsync(managerTeam, player, _session.State.CurrentDate);
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
        private async Task Back() => await _navigation.GoBackAsync();
    }
}
