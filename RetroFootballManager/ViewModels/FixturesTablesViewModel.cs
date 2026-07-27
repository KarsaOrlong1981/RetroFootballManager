using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Logging;
using RetroFootballManager.Models;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public partial class FixturesTablesViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<FixturesTablesViewModel>();

        private readonly SaveGameService _saveGame;
        private readonly GameSession _session;

        private List<Fixture> _allFixtures = [];
        private Dictionary<int, string> _teamNames = new();

        public FixturesTablesViewModel(
            IDispatcher dispatcher,
            SaveGameService saveGame,
            GameSession session)
            : base(dispatcher)
        {
            _saveGame = saveGame;
            _session = session;
            Title = "Spiele & Tabellen";
        }

        public ObservableCollection<LeagueChoice> LeagueChoices { get; } = [];
        public ObservableCollection<int> Matchdays { get; } = [];
        public ObservableCollection<StandingDisplayRow> Standings { get; } = [];
        public ObservableCollection<FixtureRow> Fixtures { get; } = [];
        public ObservableCollection<LegendItem> Legend { get; } = [];

        [ObservableProperty]
        private LeagueChoice? _selectedLeague;

        [ObservableProperty]
        private int _selectedMatchday;

        public async Task InitializeAsync()
        {
            var state = _session.State;
            if (state is null)
                return;

            try
            {
                _teamNames = _session.Teams.ToDictionary(t => t.Id, t => t.Name);
                _allFixtures = await _saveGame.GetFixturesAsync(state.Season);

                LeagueChoices.Clear();
                var leagues = await _saveGame.GetLeaguesAsync(state.Season);
                foreach (var l in leagues.OrderBy(l => l.Tier))
                    LeagueChoices.Add(new LeagueChoice(l.Tier, l.Name));

                Matchdays.Clear();
                int maxMd = _allFixtures.Count == 0 ? 0 : _allFixtures.Max(f => f.Matchday);
                for (int md = 1; md <= maxMd; md++)
                    Matchdays.Add(md);

                int myTier = _session.ManagerTeam?.LeagueTier ?? 4;
                SelectedLeague = LeagueChoices.FirstOrDefault(c => c.Tier == myTier) ?? LeagueChoices.FirstOrDefault();
                SelectedMatchday = Matchdays.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Log.Error("Spiele & Tabellen konnten nicht geladen werden.", ex);
            }
        }

        partial void OnSelectedLeagueChanged(LeagueChoice? value)
        {
            RefreshStandings();
            RefreshFixtures();
        }

        partial void OnSelectedMatchdayChanged(int value) => RefreshFixtures();

        private void RefreshStandings()
        {
            Standings.Clear();
            Legend.Clear();
            if (SelectedLeague is null)
                return;

            int tier = SelectedLeague.Tier;
            var leagueFixtures = _allFixtures.Where(f => f.LeagueTier == tier).ToList();
            var rows = StandingsCalculator.Calculate(leagueFixtures, _teamNames);

            foreach (var row in rows)
                Standings.Add(new StandingDisplayRow(row, LeagueZoneHelper.GetZone(tier, row.Position, rows.Count)));

            foreach (var item in LegendBuilder.BuildFor(tier))
                Legend.Add(item);
        }

        private void RefreshFixtures()
        {
            Fixtures.Clear();
            if (SelectedLeague is null || SelectedMatchday == 0)
                return;

            var matchFixtures = _allFixtures
                .Where(f => f.LeagueTier == SelectedLeague.Tier && f.Matchday == SelectedMatchday)
                .OrderBy(f => f.Date);

            foreach (var f in matchFixtures)
            {
                Fixtures.Add(new FixtureRow(
                    Name(f.HomeTeamId),
                    Name(f.AwayTeamId),
                    f.Played ? $"{f.HomeGoals} : {f.AwayGoals}" : f.Date.ToString("dd.MM. HH:mm").Replace(" 00:00", ""),
                    f.Date.ToString("ddd", new System.Globalization.CultureInfo("de-DE"))));
            }
        }

        private string Name(int teamId) =>
            _teamNames.TryGetValue(teamId, out var n) ? n : $"Team {teamId}";
    }

    public record LeagueChoice(int Tier, string Name);

    public record FixtureRow(string HomeName, string AwayName, string Result, string Day);
}
