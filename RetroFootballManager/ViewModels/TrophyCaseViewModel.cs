using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Data;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public record TrophyDisplayRow(string ImagePath, string Label, int Count, bool HasWonAtLeastOnce);

    public partial class TrophyCaseViewModel : BaseViewModel
    {
        private readonly GameSession _session;
        private readonly SaveGameService _saveGame;
        private readonly INavigationService _navigation;

        public TrophyCaseViewModel(IDispatcher dispatcher, GameSession session, SaveGameService saveGame, INavigationService navigation)
            : base(dispatcher)
        {
            _session = session;
            _saveGame = saveGame;
            _navigation = navigation;
            Title = "Trophäen";
        }

        public ObservableCollection<TrophyDisplayRow> Trophies { get; } = [];

        public async Task InitializeAsync()
        {
            Trophies.Clear();
            var team = _session.ManagerTeam;
            if (team is null)
                return;

            var records = await _saveGame.GetTrophiesForTeamAsync(team.Id);

            foreach (var type in TrophyDisplay.All)
            {
                var record = records.FirstOrDefault(r => r.Type == type);
                Trophies.Add(new TrophyDisplayRow(
                    TrophyDisplay.ImageFileName(type),
                    TrophyDisplay.Label(type),
                    record?.Count ?? 0,
                    record is not null));
            }
        }

        [RelayCommand]
        private Task Back() => _navigation.GoBackAsync();
    }
}
