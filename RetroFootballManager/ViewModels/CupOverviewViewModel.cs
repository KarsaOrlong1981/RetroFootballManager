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
    public record CompetitionChoice(CompetitionType Competition, string Label);
    public record GroupChoice(string Group);
    public record CupGroupStandingDisplayRow(StandingRow Row, bool Qualifies)
    {
        public Color RowColor => Qualifies ? Color.FromRgba(34, 197, 94, 55) : Colors.Transparent;
    }
    public record KoTieDisplayRow(string HomeTeamName, string AwayTeamName, string ScoreText, string WinnerText);
    public class KoRoundSection
    {
        public string RoundName { get; init; } = string.Empty;
        public List<KoTieDisplayRow> Ties { get; init; } = [];
    }

    public partial class CupOverviewViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<CupOverviewViewModel>();

        private readonly GameSession _session;
        private readonly SaveGameService _saveGame;
        private readonly PlayerStatsService _stats;
        private readonly INavigationService _navigation;

        private List<CupTie> _allTies = [];

        public CupOverviewViewModel(
            IDispatcher dispatcher, GameSession session, SaveGameService saveGame, PlayerStatsService stats,
            INavigationService navigation)
            : base(dispatcher)
        {
            _session = session;
            _saveGame = saveGame;
            _stats = stats;
            _navigation = navigation;
            Title = "Pokal-Übersicht";
        }

        public ObservableCollection<CompetitionChoice> Competitions { get; } =
        [
            new(CompetitionType.GermanCup, "Deutscher Pokal"),
            new(CompetitionType.ChampionsLeague, "Europa Pokal der Meister"),
            new(CompetitionType.EuropaCup, "Europa Cup"),
        ];

        public ObservableCollection<GroupChoice> Groups { get; } = [];
        public ObservableCollection<CupGroupStandingDisplayRow> GroupStandings { get; } = [];
        public ObservableCollection<KoRoundSection> KoBracket { get; } = [];

        public ObservableCollection<StatCategoryOption> StatCategories { get; } =
        [
            new(StatCategory.TopScorers, "Torjäger"),
            new(StatCategory.TopAssists, "Assists"),
            new(StatCategory.ScorerPoints, "Scorerpunkte"),
            new(StatCategory.YellowCards, "Gelbe Karten"),
            new(StatCategory.RedCards, "Rote Karten"),
            new(StatCategory.FewestConceded, "Wenigste Gegentore"),
        ];
        public ObservableCollection<StatRankRow> TopPlayers { get; } = [];

        [ObservableProperty] private CompetitionChoice? _selectedCompetition;
        [ObservableProperty] private GroupChoice? _selectedGroup;
        [ObservableProperty] private StatCategoryOption? _selectedStatCategory;
        [ObservableProperty] private string _statusText = string.Empty;

        public bool HasGroups => Groups.Count > 0;
        public bool HasKoBracket => KoBracket.Count > 0;

        public async Task InitializeAsync()
        {
            SelectedCompetition ??= Competitions[0];
            await ReloadAllAsync();
        }

        partial void OnSelectedCompetitionChanged(CompetitionChoice? value) => _ = ReloadAllAsync();
        partial void OnSelectedGroupChanged(GroupChoice? value) => RefreshGroupStandings();
        partial void OnSelectedStatCategoryChanged(StatCategoryOption? value) => _ = RefreshTopPlayersAsync();

        private async Task ReloadAllAsync()
        {
            var state = _session.State;
            if (state is null || SelectedCompetition is null)
                return;

            try
            {
                _allTies = await _saveGame.GetCupTiesAsync(state.Season, SelectedCompetition.Competition);

                Groups.Clear();
                foreach (var g in _allTies.Where(t => t.Round == 0 && t.Group != null)
                             .Select(t => t.Group!).Distinct().OrderBy(g => g))
                    Groups.Add(new GroupChoice(g));
                OnPropertyChanged(nameof(HasGroups));
                SelectedGroup = Groups.FirstOrDefault();
                RefreshGroupStandings();

                RefreshKoBracket();

                SelectedStatCategory ??= StatCategories.FirstOrDefault();
                await RefreshTopPlayersAsync();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to load cup overview.", ex);
                StatusText = "Daten konnten nicht geladen werden.";
            }
        }

        private void RefreshGroupStandings()
        {
            GroupStandings.Clear();
            if (SelectedGroup is null)
                return;

            var names = _session.Teams.ToDictionary(t => t.Id, t => t.Name);
            var groupTies = _allTies.Where(t => t.Round == 0 && t.Group == SelectedGroup.Group).ToList();
            var rows = GroupDrawService.CalculateGroupTable(groupTies, names);
            foreach (var row in rows)
                GroupStandings.Add(new CupGroupStandingDisplayRow(row, row.Position <= 2));
        }

        private void RefreshKoBracket()
        {
            KoBracket.Clear();
            var names = _session.Teams.ToDictionary(t => t.Id, t => t.Name);
            var tiers = _session.Teams.ToDictionary(t => t.Id, t => t.LeagueTier);
            var koTies = _allTies.Where(t => t.Round != 0).GroupBy(t => t.Round).OrderBy(g => g.Key);

            foreach (var roundGroup in koTies)
            {
                var section = new KoRoundSection { RoundName = CupMatchDayViewModel.RoundDisplayName(roundGroup.Key) };
                foreach (var pairing in roundGroup.GroupBy(t => t.MatchNumberInRound).OrderBy(g => g.Key))
                    section.Ties.Add(BuildKoRow(pairing.OrderBy(t => t.LegNumber).ToList(), names, tiers));
                KoBracket.Add(section);
            }
            OnPropertyChanged(nameof(HasKoBracket));
        }

        private static string NameWithTier(int teamId, Dictionary<int, string> names, Dictionary<int, int> tiers)
        {
            string name = names.GetValueOrDefault(teamId, "?");
            return tiers.TryGetValue(teamId, out int tier) && tier >= 1 ? $"{name} (Liga {tier})" : name;
        }

        private static KoTieDisplayRow BuildKoRow(
            List<CupTie> pairingLegs, Dictionary<int, string> names, Dictionary<int, int> tiers)
        {
            var first = pairingLegs[0];

            if (first.IsBye)
            {
                string byeName = NameWithTier(first.HomeTeamId, names, tiers);
                return new(byeName, string.Empty, string.Empty, $"{byeName} - Freilos");
            }

            string homeName = NameWithTier(first.HomeTeamId, names, tiers);
            string awayName = NameWithTier(first.AwayTeamId, names, tiers);

            if (pairingLegs.Count == 1)
            {
                var t = pairingLegs[0];
                string score = t.Played
                    ? $"{t.HomeGoals}:{t.AwayGoals}" + (t.WentToPenalties ? $" n.E. {t.PenaltyHomeGoals}:{t.PenaltyAwayGoals}" : "")
                    : "– : –";
                string winner = t.Played ? $"{NameWithTier(t.WinnerTeamId, names, tiers)} weiter" : string.Empty;
                return new(homeName, awayName, score, winner);
            }

            var leg1 = pairingLegs[0];
            var leg2 = pairingLegs.Count > 1 ? pairingLegs[1] : null;
            string leg1Score = leg1.Played ? $"{leg1.HomeGoals}:{leg1.AwayGoals}" : "– : –";
            string leg2Score = leg2 is { Played: true }
                ? $"{leg2.HomeGoals}:{leg2.AwayGoals}" + (leg2.WentToPenalties ? $" n.E. {leg2.PenaltyHomeGoals}:{leg2.PenaltyAwayGoals}" : "")
                : "– : –";
            string combined = $"Hin {leg1Score} · Rück {leg2Score}";
            string winnerText = leg2 is { Played: true }
                ? $"{NameWithTier(CupTieHelper.DetermineAggregateWinner(pairingLegs), names, tiers)} weiter"
                : string.Empty;
            return new(homeName, awayName, combined, winnerText);
        }

        private async Task RefreshTopPlayersAsync()
        {
            TopPlayers.Clear();
            var state = _session.State;
            if (state is null || SelectedCompetition is null || SelectedStatCategory is null)
                return;

            var top = await _stats.GetCompetitionTopAsync(SelectedStatCategory.Category, state.Season, SelectedCompetition.Competition);
            int rank = 1;
            foreach (var row in top)
                TopPlayers.Add(new StatRankRow(row.PlayerId, rank++, row.PlayerName, row.TeamName, row.PositionShort, row.Value));
        }

        [RelayCommand]
        private Task Back() => _navigation.GoBackAsync();
    }
}
