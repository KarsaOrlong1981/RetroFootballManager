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
    // Rating/Morale are null (shown as "?") without an Analyst on staff - see
    // TeamAssessmentEstimator and ClubViewModel.HasAnalyst.
    public record TeamOverviewRow(int TeamId, string Name, double? Rating, int? Morale, string Status = "")
    {
        public string RatingText => Rating is { } r ? $"Ø {r:0.0}" : "Ø ?";
        public string MoraleText => Morale is { } m ? $"Moral {m}" : "Moral ?";
    }

    // Detail view for a tapped team row in the club overview (own or foreign): squad
    // strength, season stat averages (from the Phase 2 TeamStats fields), form/morale, and
    // an estimated (or, for the own team, exact) rating/morale/finance snapshot - see
    // TeamAssessmentEstimator. Every estimated field is null (shown as "?") without an
    // Analyst on staff.
    public record TeamDetail(
        string Name,
        string FormationName,
        int MatchesPlayed,
        int AveragePossession,
        int AveragePassAccuracy,
        double AverageCorners,
        double AverageFreeKicks,
        double AveragePenaltys,
        double AverageOffsides,
        string Form,
        TeamAssessmentEstimate Assessment,
        ManagerProfile? ManagerProfile)
    {
        public string RatingText => Assessment.Rating is { } r ? $"{r:0.0}" : "?";
        public string MoraleText => Assessment.Morale is { } m ? $"{m}%" : "?";
        public string BalanceText => Assessment.Balance is { } balance ? $"{balance:N0} €" : "?";
        public string TransferBudgetText => Assessment.TransferBudget is { } budget ? $"{budget:N0} €" : "?";
        public string FinancialHealthText => Assessment.FinancialHealth is { } health ? $"{health}/100" : "?";
    }

    public partial class ClubViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<ClubViewModel>();

        private readonly GameSession _session;
        private readonly SaveGameService _saveGame;
        private readonly CupTieRepository _cupTieRepository;
        private readonly INavigationService _navigation;

        // Best own Analyst's TeamAssessment skill - drives every other team's Rating/Morale/
        // Finance estimate shown on this page (both the overview list and the detail dialog).
        // Null when no Analyst is on staff - every estimate then shows "?" (see
        // TeamAssessmentEstimator.Estimate).
        private int? _teamAssessmentAbility;

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

        // Team detail dialog (tapping a row in the overview).
        [ObservableProperty] private bool _isTeamDetailDialogOpen;
        [ObservableProperty] private TeamDetail? _selectedTeamDetail;

        // Manager profile dialog - opened via TeamDetailDialog's "Trainer ansehen" button,
        // always read-only here (own-profile editing lives in StaffViewModel/StaffPage).
        [ObservableProperty] private bool _isManagerProfileDialogOpen;
        [ObservableProperty] private ManagerProfile? _viewedManagerProfile;

        // Analyst panel - mirrors the Director of Football panel on the Merchandise page.
        [ObservableProperty] private bool _hasAnalyst;
        [ObservableProperty] private string _analystName = string.Empty;
        [ObservableProperty] private string _analystImagePath = string.Empty;
        [ObservableProperty] private int _analystTeamAssessment;


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

            var analyst = team.Employees
                .Where(e => e.EmployeeType == EmployeeType.Analyst)
                .OrderByDescending(e => e.TeamAssessment)
                .FirstOrDefault();
            HasAnalyst = analyst is not null;
            if (analyst is not null)
            {
                AnalystName = analyst.Name;
                AnalystImagePath = analyst.ImagePath ?? string.Empty;
                AnalystTeamAssessment = analyst.TeamAssessment;
            }
            _teamAssessmentAbility = analyst?.TeamAssessment;

            OtherTeams.Clear();
            foreach (var other in _session.Teams.Where(t => t.Id != team.Id)
                         .OrderByDescending(t => TeamStrengthCalculator.Calculate(t, isHome: false).Overall))
                OtherTeams.Add(BuildOverviewRow(other, state));

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

        [RelayCommand]
        private Task OpenStaff() => _navigation.GoToAsync("staff");

        [RelayCommand]
        private void ShowTeamDetail(int teamId)
        {
            var team = _session.Teams.FirstOrDefault(t => t.Id == teamId);
            var manager = _session.ManagerTeam;
            var state = _session.State;
            if (team is null || manager is null || state is null)
                return;

            var strength = TeamStrengthCalculator.Calculate(team, isHome: false);
            var stats = team.Statistics;
            double realRating = strength.Overall;
            int realMorale = stats?.Morale ?? 50;

            TeamAssessmentEstimate assessment;
            if (team.Id == manager.Id)
            {
                assessment = new TeamAssessmentEstimate(
                    realRating, realMorale, team.Finances?.CurrentBalance, team.Finances?.TransferBudget,
                    team.Finances?.FinancialHealth, IsExact: true, AccuracyLabel: "Exakt");
            }
            else
            {
                var rng = new Random(HashCode.Combine(team.Id, state.Season, state.CurrentDate.Month));
                assessment = TeamAssessmentEstimator.Estimate(realRating, realMorale, team.Finances, _teamAssessmentAbility, rng);
            }

            SelectedTeamDetail = new TeamDetail(
                team.Name,
                FormationCatalog.GetByName(team.FormationName, team.TacticalOrientation).Name,
                stats?.MatchesPlayed ?? 0,
                stats?.AveragePossessions ?? 0,
                stats?.AveragePassAccuracy ?? 0,
                stats?.AverageCorners ?? 0,
                stats?.AverageFreeKicks ?? 0,
                stats?.AveragePenaltys ?? 0,
                stats?.AverageOffsides ?? 0,
                stats is null ? string.Empty : new string(stats.Form.ToArray()),
                assessment,
                team.ManagerProfile);
            IsTeamDetailDialogOpen = true;
        }

        // Shared by InitializeAsync's default list and GroupBySettings - own team is always
        // exact, every other team goes through TeamAssessmentEstimator (null ability = every
        // field "?"). Deterministic per (team, season, month) so re-sorting/re-filtering within
        // the same visit doesn't jitter the displayed numbers.
        private TeamOverviewRow BuildOverviewRow(Team other, GameState state, string status = "")
        {
            double realRating = TeamStrengthCalculator.Calculate(other, isHome: false).Overall;
            int realMorale = other.Statistics?.Morale ?? 50;

            if (other.Id == state.ManagerTeamId)
                return new TeamOverviewRow(other.Id, other.Name, realRating, realMorale, status);

            var rng = new Random(HashCode.Combine(other.Id, state.Season, state.CurrentDate.Month));
            var estimate = TeamAssessmentEstimator.Estimate(realRating, realMorale, other.Finances, _teamAssessmentAbility, rng);
            return new TeamOverviewRow(other.Id, other.Name, estimate.Rating, estimate.Morale, status);
        }

        [RelayCommand]
        private void CloseTeamDetail() => IsTeamDetailDialogOpen = false;

        [RelayCommand]
        private void ShowManagerProfile()
        {
            if (SelectedTeamDetail?.ManagerProfile is null)
                return;

            ViewedManagerProfile = SelectedTeamDetail.ManagerProfile;
            IsManagerProfileDialogOpen = true;
        }

        [RelayCommand]
        private void CloseManagerProfile() => IsManagerProfileDialogOpen = false;

        private async Task<ObservableCollection<TeamOverviewRow>> GroupBySettings()
        {
            var resultList = new ObservableCollection<TeamOverviewRow>();
            var state = _session.State;
            if (state is null)
                return resultList;

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

                // Sorting always uses the REAL underlying values (server-side truth), even
                // though the displayed number itself is masked to "?" without an Analyst -
                // matches how a manager can reasonably gauge relative strength just by
                // following the league, without needing exact figures.
                var ordered = SelectedGroupConditionType switch
                {
                    GroupConditionType.Rating => teamsInTier.OrderByDescending(t => TeamStrengthCalculator.Calculate(t, isHome: false).Overall),
                    GroupConditionType.Moral => teamsInTier.OrderByDescending(t => t.Statistics?.Morale ?? 50),
                    _ => teamsInTier.OrderBy(t => t.Name)
                 };

                foreach (var team in ordered)
                    resultList.Add(BuildOverviewRow(team, state));
            }
            else
            {
                // group by Cups


                var competitionType = MapToCompetitionType(SelectedKind);
                var ties = await _cupTieRepository.GetBySeasonAsync(state.Season, competitionType);

                var participantIds = ties.SelectMany(t => new[] { t.HomeTeamId, t.AwayTeamId }).Where(id => id != 0).ToHashSet();
                var participants = _session.Teams.Where(t => participantIds.Contains(t.Id));

                var ordered = SelectedGroupConditionType switch
                {
                    GroupConditionType.Rating => participants.OrderByDescending(t => TeamStrengthCalculator.Calculate(t, isHome: false).Overall),
                    GroupConditionType.Moral => participants.OrderByDescending(t => t.Statistics?.Morale ?? 50),
                    _ => participants.OrderBy(t => t.Name)
                };

                foreach (var team in ordered)
                {
                    var status = CupParticipationService.GetStatus(team.Id, ties);
                    resultList.Add(BuildOverviewRow(team, state, GetStatusText(status)));
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
