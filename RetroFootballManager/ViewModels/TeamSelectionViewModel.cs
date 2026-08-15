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
    public partial class TeamSelectionViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<TeamSelectionViewModel>();

        private readonly SaveGameService _saveGame;
        private readonly CareerService _career;
        private readonly GameSession _session;
        private readonly INavigationService _navigation;

        private List<Team> _allTeams = [];
        private List<League> _leagues = [];

        public TeamSelectionViewModel(
            IDispatcher dispatcher,
            SaveGameService saveGame,
            CareerService career,
            GameSession session,
            INavigationService navigation)
            : base(dispatcher)
        {
            _saveGame = saveGame;
            _career = career;
            _session = session;
            _navigation = navigation;
            Title = "Team auswählen";
        }

        public ObservableCollection<LeagueOption> Leagues { get; } = [];
        public ObservableCollection<TeamOption> Teams { get; } = [];

        [ObservableProperty]
        private LeagueOption? _selectedLeague;

        [ObservableProperty]
        private TeamOption? _selectedTeam;

        [ObservableProperty]
        private string _careerInfo = string.Empty;

        [ObservableProperty]
        private string _busyText = string.Empty;

        [ObservableProperty]
        private string _selectionHint = string.Empty;

        [ObservableProperty]
        private DifficultyOption? _selectedDifficulty;

        private bool _revertingSelection;

        public ObservableCollection<DifficultyOption> DifficultyOptions { get; } =
        [
            new(Difficulty.Easy, "Leicht", "COM-Teams entwickeln sich langsamer."),
            new(Difficulty.Normal, "Normal", "Ausgeglichenes Tempo."),
            new(Difficulty.Hard, "Schwer", "COM-Teams entwickeln/managen sich aktiver."),
        ];

        public void Initialize()
        {
            var (leagues, teams) = UniverseGenerator.CreateUniverse(season: 1, random: new Random());
            _leagues = leagues;
            _allTeams = teams;
            _session.PendingUniverse = (leagues, teams);

            int highest = _career.HighestUnlockedTier;
            CareerInfo = highest == 1
                ? $"Alle Ligen freigeschaltet ({_career.Points} Punkte)."
                : $"{_career.Points} Punkte – noch {_career.PointsToNextTier()} bis zur nächsten Liga.";

            Leagues.Clear();
            foreach (var league in leagues.OrderBy(l => l.Tier))
            {
                bool unlocked = _career.IsTierUnlocked(league.Tier);
                Leagues.Add(new LeagueOption(
                    league.Tier,
                    league.Name,
                    unlocked,
                    unlocked ? "verfügbar" : $"gesperrt – {ThresholdFor(league.Tier)} Punkte nötig"));
            }

            SelectedLeague = Leagues.FirstOrDefault(l => l.Tier == highest) ?? Leagues.LastOrDefault();
            SelectedDifficulty ??= DifficultyOptions.First(d => d.Value == Difficulty.Normal);
        }

        partial void OnSelectedLeagueChanged(LeagueOption? value)
        {
            Teams.Clear();
            SelectedTeam = null;
            if (value is null)
                return;

            SelectionHint = value.IsUnlocked
                ? string.Empty
                : $"{value.Name} ist noch gesperrt – nur ansehen, nicht auswählbar.";

            foreach (var team in _allTeams
                         .Where(t => t.LeagueTier == value.Tier)
                         .OrderByDescending(t => t.AverageRating))
            {
                Teams.Add(new TeamOption(team, Math.Round(team.AverageRating, 1), value.IsUnlocked));
            }
        }

        partial void OnSelectedTeamChanged(TeamOption? value)
        {
            if (_revertingSelection)
                return;

            if (value is not null && !value.IsSelectable)
            {
                SelectionHint = "Diese Liga ist noch nicht freigeschaltet.";
                _revertingSelection = true;
                SelectedTeam = null;
                _revertingSelection = false;
            }

            StartCommand.NotifyCanExecuteChanged();
        }

        private bool CanStart() => SelectedTeam is not null && (SelectedLeague?.IsUnlocked ?? false);

        [RelayCommand(CanExecute = nameof(CanStart))]
        private async Task Start()
        {
            if (IsBusy || SelectedTeam is null) return;
            IsBusy = true;
            BusyText = "Neues Spiel wird angelegt …";
            try
            {
                var managerTeam = SelectedTeam.Team;
                if (_session.PendingManagerProfile is not null)
                    managerTeam.ManagerProfile = _session.PendingManagerProfile;

                var state = await _saveGame.StartNewCareerAsync(
                    saveName: managerTeam.Name,
                    season: 1,
                    _leagues,
                    _allTeams,
                    managerTeam,
                    seasonStart: new DateTime(2026, 8, 1),
                    difficulty: (SelectedDifficulty ?? DifficultyOptions[1]).Value);

                _session.State = state;
                _session.Teams = await _saveGame.GetAllTeamsAsync();
                _session.PendingUniverse = null;
                _session.PendingManagerProfile = null;

                await _navigation.GoToRootAsync("mainmenu");
            }
            catch (Exception ex)
            {
                Log.Error("Could not create new game.", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static int ThresholdFor(int tier) => tier switch
        {
            3 => CareerService.Tier3Threshold,
            2 => CareerService.Tier2Threshold,
            1 => CareerService.Tier1Threshold,
            _ => 0,
        };
    }

    public record LeagueOption(int Tier, string Name, bool IsUnlocked, string StatusLabel);

    public record TeamOption(Team Team, double AverageRating, bool IsSelectable)
    {
        public string Name => Team.Name;
        public string ShortName => Team.ShortName;
    }

    public record DifficultyOption(Difficulty Value, string Label, string Description);
}
