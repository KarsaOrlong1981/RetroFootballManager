using CommunityToolkit.Mvvm.ComponentModel;
using RetroFootballManager.Common;
using RetroFootballManager.Models;

namespace RetroFootballManager.ViewModels
{
    // One article row on the Merchandise page. Stock/price come from MerchandiseInventory;
    // BuyQuantity/SellPrice are bound to SteppedAmountInput controls (+/- only, no textbox,
    // shared step size via MerchandiseViewModel.BuyStepSize/PriceStepSize - same pattern as
    // NegotiationDialogViewModel.NegotiationStepSize).
    public partial class MerchandiseArticleRow : ObservableObject
    {
        private readonly Action<MerchandiseArticleRow> _onPriceChanged;

        public MerchandiseArticleType Type { get; }
        public string DisplayName { get; }
        public string SeasonText { get; }
        public int WholesaleCost { get; }
        public string WholesaleCostText { get; }

        [ObservableProperty] private int _stock;
        [ObservableProperty] private double _sellPrice;
        [ObservableProperty] private double _buyQuantity;
        [ObservableProperty] private double _sellBackQuantity;
        [ObservableProperty] private int _lastWeekUnitsSold;
        [ObservableProperty] private int _lastWeekRevenue;
        [ObservableProperty] private int _totalUnitsSold;
        [ObservableProperty] private int _totalRevenue;

        // Inline feedback right next to this row's own buy/Rückkauf buttons - a page-wide
        // StatusText at the bottom of a long scrollable list of 16 articles is easy to miss
        // (reported by the user: clicking "Kaufen" gave no visible feedback).
        [ObservableProperty] private string _feedbackText = string.Empty;

        // Director of Football's purchase recommendation - empty until "Um Einschätzung
        // bitten" is used (requires a DoF on staff, see MerchandiseViewModel.HasDirectorOfFootball).
        [ObservableProperty] private string _recommendationText = string.Empty;

        public MerchandiseArticleRow(
            MerchandiseArticleDefinition def, MerchandiseArticleStock stock,
            Action<MerchandiseArticleRow> onPriceChanged, string? displayNameOverride = null)
        {
            Type = def.Type;
            DisplayName = displayNameOverride ?? def.DisplayName;
            WholesaleCost = def.WholesaleCost;
            WholesaleCostText = $"{def.WholesaleCost:N0} € / Stück";
            SeasonText = def.Season switch
            {
                MerchandiseSeason.Winter => "❄ Winterartikel",
                MerchandiseSeason.Summer => "☀ Sommerartikel",
                _ => "Ganzjährig",
            };

            _stock = stock.Stock;
            _sellPrice = stock.SellPrice;
            _lastWeekUnitsSold = stock.LastWeekUnitsSold;
            _lastWeekRevenue = stock.LastWeekRevenue;
            _totalUnitsSold = stock.TotalUnitsSold;
            _totalRevenue = stock.TotalRevenue;

            _onPriceChanged = onPriceChanged;
        }

        public string StockText => $"{Stock:N0} auf Lager";
        public string MarginText => $"{SellPrice - WholesaleCost:N0} € Marge/Stück";
        public string BuyCostText => $"Kosten: {BuyQuantity * WholesaleCost:N0} €";
        public bool CanBuy => BuyQuantity > 0;
        public string SellBackRefundText => $"Erstattung: {SellBackQuantity * WholesaleCost * MerchandiseService.SellBackRefundFraction:N0} €";
        public bool CanSellBack => SellBackQuantity > 0;
        public string LastWeekText => $"Letzte Woche: {LastWeekUnitsSold:N0} Stk. · {LastWeekRevenue:N0} €";
        public string TotalText => $"Saison gesamt: {TotalUnitsSold:N0} Stk. · {TotalRevenue:N0} €";

        partial void OnSellPriceChanged(double value)
        {
            OnPropertyChanged(nameof(MarginText));
            _onPriceChanged(this);
        }

        partial void OnBuyQuantityChanged(double value)
        {
            OnPropertyChanged(nameof(BuyCostText));
            OnPropertyChanged(nameof(CanBuy));
        }

        partial void OnSellBackQuantityChanged(double value)
        {
            OnPropertyChanged(nameof(SellBackRefundText));
            OnPropertyChanged(nameof(CanSellBack));
        }

        partial void OnStockChanged(int value) => OnPropertyChanged(nameof(StockText));
    }
}
