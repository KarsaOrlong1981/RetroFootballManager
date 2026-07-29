using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Common;
using RetroFootballManager.Core.Models;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Logging;
using RetroFootballManager.Models;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public record TeamOverviewRow(string Name, double AvgRating, double AvgMoral, string Status = "");

    public partial class ClubViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<ClubViewModel>();

        private readonly GameSession _session;
        private readonly SaveGameService _saveGame;
        private readonly CupTieRepository _cupTieRepository;
        private readonly INavigationService _navigation;

        public ClubViewModel(IDispatcher dispatcher, GameSession session, SaveGameService saveGame, INavigationService navigation, CupTieRepository cupTieRepository)
            : base(dispatcher)
        {
            _session = session;
            _saveGame = saveGame;
            _navigation = navigation;
            _cupTieRepository = cupTieRepository;
            Title = "Verein";
        }

        public ObservableCollection<TeamOverviewRow> OtherTeams { get; } = [];
        public ObservableCollection<CompetitionKind> CompetitionKinds { get; } = [];
        public ObservableCollection<GroupConditionType> GroupConditionTypes { get; } = [];

        [ObservableProperty] private string _clubName = string.Empty;
        [ObservableProperty] private string _leagueTierText = string.Empty;
        [ObservableProperty] private string _leaguePositionText = string.Empty;
        [ObservableProperty] private string _stadiumSummary = string.Empty;
        [ObservableProperty] private string _ratingComparisonText = string.Empty;
        [ObservableProperty] private string _moraleComparisonText = string.Empty;
        [ObservableProperty] private CompetitionKind _selectedKind;
        [ObservableProperty] private GroupConditionType _selectedGroupConditionType;


        public string DisplayCompetitionKind => GetCompetitionKindOutput();


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

           foreach (var kind in Enum.GetValues<CompetitionKind>())
                CompetitionKinds.Add(kind);
           foreach (var type in  Enum.GetValues<GroupConditionType>())
                GroupConditionTypes.Add(type);
            SelectedGroupConditionType = GroupConditionType.Rating;
            SelectedKind = CompetitionKind.Tier1;
            await ApplyGroupingAsync();
        }

        [RelayCommand]
        private Task Back() => _navigation.GoBackAsync();

        private async Task<ObservableCollection<TeamOverviewRow>> GroupBySettings()
        {
            var resultList = new ObservableCollection<TeamOverviewRow>();

            // group by League (Moral/Rating)
            if (SelectedKind == CompetitionKind.Tier1 || SelectedKind == CompetitionKind.Tier2 || SelectedKind == CompetitionKind.Tier3 || SelectedKind == CompetitionKind.Tier4)
            {
                var tierNumber = SelectedKind switch
                {
                    CompetitionKind.Tier1 => 1,
                    CompetitionKind.Tier2 => 2,
                    CompetitionKind.Tier3 => 3,
                    CompetitionKind.Tier4 => 4,
                    _ => 0
                };

                var teamsInTier = _session.Teams.Where(t => t.LeagueTier == tierNumber);

                var ordered = SelectedGroupConditionType switch
                {
                    GroupConditionType.Rating => teamsInTier.OrderByDescending(t => t.AverageRating),
                    GroupConditionType.Moral => teamsInTier.OrderByDescending(t => t.Statistics?.Morale ?? 50),
                    _ => teamsInTier.OrderBy(t => t.Name)
                 };

                foreach(var team in ordered)
                {
                    double avgMoral = team.Players.Count > 0 ? team.Players.Average(p => p.Moral) : 0;
                    resultList.Add(new TeamOverviewRow(team.Name, team.AverageRating, avgMoral));
                }
            }
            else
            {
                // group by Cups

                var state = _session.State;
                if (state == null)
                    return resultList;

                var competitionType = MapToCompetitionType(SelectedKind);
                var ties = await _cupTieRepository.GetBySeasonAsync(state.Season, competitionType);

                var participantIds = ties.SelectMany(t => new[] { t.HomeTeamId, t.AwayTeamId }).Where(id => id != 0).ToHashSet();
                var participants = _session.Teams.Where(t => participantIds.Contains(t.Id));

                var ordered = SelectedGroupConditionType switch
                {
                    GroupConditionType.Rating => participants.OrderByDescending(t => t.AverageRating),
                    GroupConditionType.Moral => participants.OrderByDescending(t => t.Statistics?.Morale ?? 50),
                    _ => participants.OrderBy(t => t.Name)
                };

                foreach (var team in ordered)
                {
                    double avgMoral = team.Players.Count > 0 ? team.Players.Average(p => p.Moral) : 0;
                    var status = CupParticipationService.GetStatus(team.Id, ties);
                    resultList.Add(new TeamOverviewRow(team.Name, team.AverageRating, avgMoral, GetStatusText(status)));
                }
            }

            return resultList;
        }

        private static CompetitionType MapToCompetitionType(CompetitionKind kind) => kind switch
        {
            CompetitionKind.GermanCup => CompetitionType.GermanCup,
            CompetitionKind.EuropeanMasterCup => CompetitionType.ChampionsLeague,
            CompetitionKind.EuropeanCup => CompetitionType.EuropaCup,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Kein Pokalwettbewerb.")
        };

        private static string GetStatusText(CupParticipationStatus status) => status switch
        {
            CupParticipationStatus.StillIn => "im Wettbewerb",
            CupParticipationStatus.Eliminated => "ausgeschieden",
            CupParticipationStatus.Won => "Sieger",
            _ => "nicht dabei"
        };

        private string GetCompetitionKindOutput()
        {
            var output = SelectedKind switch
            {
                CompetitionKind.EuropeanCup => "Europa Pokal",
                CompetitionKind.EuropeanMasterCup => "Europa Pokal der Meister",
                CompetitionKind.GermanCup => "Deutscher Pokal",
                CompetitionKind.Tier1 => "1.Liga",
                CompetitionKind.Tier2 => "2.Liga",
                CompetitionKind.Tier3 => "3.Liga",
                CompetitionKind.Tier4 => "4.Liga",
                _ => string.Empty
            };

            return output;
        }

        partial void OnSelectedKindChanged(CompetitionKind value)
        {
            OnPropertyChanged(nameof(DisplayCompetitionKind));
            _ = ApplyGroupingAsync();
        }

        partial void OnSelectedGroupConditionTypeChanged(GroupConditionType value)
        {
            _ = ApplyGroupingAsync();
        }

        private async Task ApplyGroupingAsync()
        {
            try
            {
                var grouped = await GroupBySettings();
                OtherTeams.Clear();
                foreach (var row in grouped)
                    OtherTeams.Add(row);
            }
            catch (Exception ex)
            {
                Log.Error("Could not group teams.", ex);
            }
        }
    }
}
