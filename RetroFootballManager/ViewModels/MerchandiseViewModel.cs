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
    public partial class MerchandiseViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<MerchandiseViewModel>();

        private readonly GameSession _session;
        private readonly SaveGameService _saveGame;
        private readonly MerchandiseService _merchandise;
        private readonly INavigationService _navigation;

        private Team? _team;
        private MerchandiseInventory? _inventory;
        private double _performanceFactor = 1.0;
        private double? _bestPlayerRating;

        public MerchandiseViewModel(
            IDispatcher dispatcher, GameSession session, SaveGameService saveGame,
            MerchandiseService merchandise, INavigationService navigation)
            : base(dispatcher)
        {
            _session = session;
            _saveGame = saveGame;
            _merchandise = merchandise;
            _navigation = navigation;
            Title = "Merchandise";
        }

        public ObservableCollection<MerchandiseArticleRow> Articles { get; } = [];

        // Shared by every row's buy-quantity/price/sell-back stepper - one click means the
        // same step everywhere, adjustable via a PresetStepInput (see NegotiationDialogViewModel).
        [ObservableProperty] private double _buyStepSize = 10;
        [ObservableProperty] private double _priceStepSize = 1;
        [ObservableProperty] private double _sellBackStepSize = 10;

        [ObservableProperty] private string _memberCountText = string.Empty;
        [ObservableProperty] private string _leaguePositionText = string.Empty;
        [ObservableProperty] private string _performanceFactorText = string.Empty;

        [ObservableProperty] private string _seasonIncomeText = string.Empty;
        [ObservableProperty] private string _seasonExpenseText = string.Empty;
        [ObservableProperty] private string _seasonNetText = string.Empty;

        // Director of Football - drives BOTH the purchase recommendation below AND whether a
        // membership campaign can be launched at all (see ClubMembershipService.
        // HasDirectorOfFootball) - deliberately makes hiring one worth it.
        [ObservableProperty] private bool _hasDirectorOfFootball;
        [ObservableProperty] private string _directorOfFootballName = string.Empty;
        [ObservableProperty] private string _directorOfFootballImagePath = string.Empty;
        [ObservableProperty] private int _directorOfFootballFinancialManagement;

        // Membership recruitment campaign - feedback/progress shown inline in that section,
        // not via a page-wide status text (same "easy to miss" lesson as the Buy feedback).
        [ObservableProperty] private string _campaignCostText = string.Empty;
        [ObservableProperty] private bool _canLaunchCampaign;
        [ObservableProperty] private bool _isCampaignActive;
        [ObservableProperty] private string _campaignProgressText = string.Empty;
        [ObservableProperty] private string _campaignFeedbackText = string.Empty;

        public async Task InitializeAsync()
        {
            _team = _session.ManagerTeam;
            if (_team?.Finances is null)
                return;

            _inventory = await _merchandise.GetOrCreateInventoryAsync(_team.Id);
            _bestPlayerRating = _team.Players.Count > 0 ? _team.Players.Max(p => p.Rating) : null;
            string? bestPlayerName = _team.Players.OrderByDescending(p => p.Rating).FirstOrDefault()?.Name;

            Articles.Clear();
            foreach (var stock in _inventory.Articles)
            {
                var def = MerchandiseCatalog.Get(stock.Type);
                string? nameOverride = stock.Type == MerchandiseArticleType.StarSpielerTrikot && bestPlayerName is not null
                    ? $"Star-Trikot ({bestPlayerName})"
                    : null;
                Articles.Add(new MerchandiseArticleRow(def, stock, OnPriceChanged, nameOverride));
            }

            var dof = _team.Employees.FirstOrDefault(e => e.EmployeeType == EmployeeType.DirectorOfFootball);
            HasDirectorOfFootball = dof is not null;
            if (dof is not null)
            {
                DirectorOfFootballName = dof.Name;
                DirectorOfFootballImagePath = dof.ImagePath ?? string.Empty;
                DirectorOfFootballFinancialManagement = dof.FinancialManagement;
            }

            await RefreshSummaryAsync();
        }

        private async Task RefreshSummaryAsync()
        {
            var finances = _team!.Finances!;
            MemberCountText = $"{finances.ClubMembers:N0} Vereinsmitglieder";

            StandingRow? row = null;
            int leagueSize = 18;
            var currentDate = _session.State?.CurrentDate ?? DateTime.Today;
            if (_session.State is { } state)
            {
                try
                {
                    var fixtures = await _saveGame.GetFixturesAsync(state.Season);
                    var leagueFixtures = fixtures.Where(f => f.LeagueTier == _team.LeagueTier).ToList();
                    var names = _session.Teams.ToDictionary(t => t.Id, t => t.Name);
                    var standings = StandingsCalculator.Calculate(leagueFixtures, names);
                    row = standings.FirstOrDefault(s => s.TeamId == _team.Id);
                    leagueSize = Math.Max(standings.Count, 1);
                }
                catch (Exception ex)
                {
                    Log.Error("Could not determine standings for the merchandise performance factor.", ex);
                }
            }

            LeaguePositionText = row is null ? "–" : $"Tabellenplatz {row.Position} von {leagueSize}";
            _performanceFactor = MerchandiseSalesCalculator.PerformanceFactor(row, leagueSize);
            PerformanceFactorText = $"Nachfrage-Faktor (Form/Tabellenplatz): {_performanceFactor:P0}";

            SeasonIncomeText = $"{finances.MerchandiseArticleIncome:N0} €";
            SeasonExpenseText = $"{finances.MerchandiseArticleExpense:N0} €";
            SeasonNetText = $"{finances.MerchandiseArticleIncome - finances.MerchandiseArticleExpense:N0} €";

            int cost = ClubMembershipService.GetCampaignCost(_team.LeagueTier, finances.MembershipFeePerMember);
            CampaignCostText = $"{cost:N0} €";
            IsCampaignActive = ClubMembershipService.IsCampaignActive(finances, currentDate);
            CanLaunchCampaign = !IsCampaignActive && HasDirectorOfFootball && finances.CurrentBalance >= cost;

            CampaignProgressText = IsCampaignActive
                ? $"Kampagne läuft noch {Math.Max(0, (finances.MembershipCampaignEndDate!.Value - currentDate).Days)} Tage - " +
                  $"bisher {finances.MembershipCampaignAppliedGain:N0} von {finances.MembershipCampaignTotalGain:N0} neuen Mitgliedern"
                : string.Empty;
        }

        // No "Bestätigen" step anywhere on this page - every action below applies AND
        // persists immediately (fire-and-forget from the property-changed callback for price,
        // awaited directly for the button commands).

        private void OnPriceChanged(MerchandiseArticleRow row)
        {
            if (_inventory is null)
                return;

            MerchandiseService.SetPrice(_inventory, row.Type, (int)Math.Round(row.SellPrice));
            _ = PersistInventoryOnlyAsync();
        }

        // Price changes only touch the inventory row (not Finances/roster), so only the
        // small inventory JSON blob needs saving - not the full team via SaveTeamProgressAsync.
        private async Task PersistInventoryOnlyAsync()
        {
            if (_inventory is null)
                return;

            try
            {
                await _merchandise.SaveInventoryAsync(_inventory);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to save merchandise price change.", ex);
            }
        }

        [RelayCommand]
        private async Task Buy(MerchandiseArticleRow row)
        {
            if (_team is null || _inventory is null || _session.State is null)
                return;

            if (row.BuyQuantity <= 0)
            {
                row.FeedbackText = "Bitte zuerst eine Menge wählen.";
                return;
            }

            if (IsBusy)
                return;

            int quantity = (int)Math.Round(row.BuyQuantity);
            bool applied = MerchandiseService.TryBuyStock(_team, _inventory, row.Type, quantity);
            if (!applied)
            {
                row.FeedbackText = "Nicht genug Geld für diesen Einkauf.";
                return;
            }

            row.Stock = _inventory.Articles.First(a => a.Type == row.Type).Stock;
            row.BuyQuantity = 0;
            row.FeedbackText = $"✓ {quantity:N0} x eingekauft.";

            await PersistPurchaseAsync();
        }

        [RelayCommand]
        private async Task SellBack(MerchandiseArticleRow row)
        {
            if (_team is null || _inventory is null || _session.State is null)
                return;

            if (row.SellBackQuantity <= 0)
            {
                row.FeedbackText = "Bitte zuerst eine Menge wählen.";
                return;
            }

            if (IsBusy)
                return;

            int quantity = (int)Math.Round(row.SellBackQuantity);
            bool applied = MerchandiseService.TrySellBackStock(_team, _inventory, row.Type, quantity);
            if (!applied)
            {
                row.FeedbackText = "So viele Stück hast du gar nicht auf Lager.";
                return;
            }

            row.Stock = _inventory.Articles.First(a => a.Type == row.Type).Stock;
            row.SellBackQuantity = 0;
            int refund = (int)Math.Round(quantity * row.WholesaleCost * MerchandiseService.SellBackRefundFraction);
            row.FeedbackText = $"↩ {quantity:N0} x zurückverkauft (+{refund:N0} €).";

            await PersistPurchaseAsync();
        }

        // Shared save path for Buy/SellBack - both touch inventory stock AND Finances.
        private async Task PersistPurchaseAsync()
        {
            IsBusy = true;
            try
            {
                await _merchandise.SaveInventoryAsync(_inventory!);
                await _saveGame.SaveTeamProgressAsync(_session.State!, _team!);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to save merchandise stock change.", ex);
            }
            finally
            {
                IsBusy = false;
            }

            await RefreshSummaryAsync();
        }

        [RelayCommand]
        private void AskForRecommendation()
        {
            if (_team?.Finances is null || _inventory is null || !HasDirectorOfFootball)
                return;

            var currentDate = _session.State?.CurrentDate ?? DateTime.Today;
            var articlesByType = _inventory.Articles.ToDictionary(a => a.Type);

            foreach (var row in Articles)
            {
                if (!articlesByType.TryGetValue(row.Type, out var article))
                    continue;

                var rec = MerchandiseAdvisor.Recommend(
                    article, _team.Finances.ClubMembers, _performanceFactor, currentDate, _bestPlayerRating,
                    DirectorOfFootballFinancialManagement);

                row.RecommendationText = rec.IsExact
                    ? $"Empfehlung: {rec.RecommendedMin:N0} Stück"
                    : rec.RecommendedMax <= 0
                        ? "Empfehlung: nichts nachkaufen"
                        : $"Empfehlung: ca. {rec.RecommendedMin:N0}-{rec.RecommendedMax:N0} Stück";
            }
        }

        [RelayCommand]
        private Task OpenStaff() => _navigation.GoToAsync("staff");

        [RelayCommand]
        private async Task LaunchCampaign()
        {
            if (IsBusy || _team is null || _session.State is null)
                return;

            if (!HasDirectorOfFootball)
            {
                CampaignFeedbackText = "Ohne Sportdirektor keine Kampagne möglich.";
                return;
            }

            bool applied = ClubMembershipService.TryLaunchCampaign(_team, _session.State.CurrentDate, _performanceFactor, Random.Shared);
            CampaignFeedbackText = applied
                ? "Werbekampagne gestartet - neue Mitglieder kommen über die Laufzeit nach und nach dazu."
                : ClubMembershipService.IsCampaignActive(_team.Finances!, _session.State.CurrentDate)
                    ? "Es läuft bereits eine Kampagne."
                    : "Nicht genug Geld für eine Werbekampagne.";

            if (!applied)
                return;

            IsBusy = true;
            try
            {
                await _saveGame.SaveTeamProgressAsync(_session.State, _team);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to save membership campaign.", ex);
                CampaignFeedbackText = "Speichern fehlgeschlagen.";
            }
            finally
            {
                IsBusy = false;
            }

            await RefreshSummaryAsync();
        }
    }
}
