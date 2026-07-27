using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Logging;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public record TeamOverviewRow(string Name, double AvgRating, double AvgMoral);

    public partial class ClubViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<ClubViewModel>();

        private readonly GameSession _session;
        private readonly SaveGameService _saveGame;
        private readonly INavigationService _navigation;

        public ClubViewModel(IDispatcher dispatcher, GameSession session, SaveGameService saveGame, INavigationService navigation)
            : base(dispatcher)
        {
            _session = session;
            _saveGame = saveGame;
            _navigation = navigation;
            Title = "Verein";
        }

        public ObservableCollection<TeamOverviewRow> OtherTeams { get; } = [];

        [ObservableProperty] private string _clubName = string.Empty;
        [ObservableProperty] private string _leagueTierText = string.Empty;
        [ObservableProperty] private string _leaguePositionText = string.Empty;
        [ObservableProperty] private string _stadiumSummary = string.Empty;
        [ObservableProperty] private string _ratingComparisonText = string.Empty;
        [ObservableProperty] private string _moraleComparisonText = string.Empty;

        public async Task InitializeAsync()
        {
            var team = _session.ManagerTeam;
            var state = _session.State;
            if (team is null || state is null)
                return;

            ClubName = team.Name;
            LeagueTierText = $"Liga {team.LeagueTier}";
            StadiumSummary = team.Stadium is null
                ? "Kein Stadion."
                : $"{team.Stadium.Name} · {team.Stadium.Capacity:N0} Plätze · Komfort {team.Stadium.ComfortLevel}/5";

            try
            {
                var fixtures = await _saveGame.GetFixturesAsync(state.Season);
                var leagueFixtures = fixtures.Where(f => f.LeagueTier == team.LeagueTier).ToList();
                var names = _session.Teams.ToDictionary(t => t.Id, t => t.Name);
                var standings = StandingsCalculator.Calculate(leagueFixtures, names);
                var row = standings.FirstOrDefault(s => s.TeamId == team.Id);
                LeaguePositionText = row is null ? "–" : $"Tabellenplatz {row.Position} von {standings.Count}";
            }
            catch (Exception ex)
            {
                Log.Error("Could not determine league position.", ex);
                LeaguePositionText = "–";
            }

            OtherTeams.Clear();
            foreach (var other in _session.Teams.Where(t => t.Id != team.Id).OrderByDescending(t => t.AverageRating))
            {
                double avgMoral = other.Players.Count > 0 ? other.Players.Average(p => p.Moral) : 0;
                OtherTeams.Add(new TeamOverviewRow(other.Name, other.AverageRating, avgMoral));
            }

            var leagueTeams = _session.Teams.Where(t => t.LeagueTier == team.LeagueTier).ToList();
            double leagueAvgRating = leagueTeams.Count > 0 ? leagueTeams.Average(t => t.AverageRating) : team.AverageRating;
            RatingComparisonText = $"Rating: {team.AverageRating:0.0} (Liga {team.LeagueTier} Schnitt: {leagueAvgRating:0.0})";

            int ownMorale = team.Statistics?.Morale ?? 50;
            double leagueAvgMorale = leagueTeams.Count > 0
                ? leagueTeams.Average(t => t.Statistics?.Morale ?? 50)
                : ownMorale;
            MoraleComparisonText = $"Moral: {ownMorale}% (Liga {team.LeagueTier} Schnitt: {leagueAvgMorale:0}%)";
        }

        [RelayCommand]
        private Task Back() => _navigation.GoBackAsync();
    }
}
