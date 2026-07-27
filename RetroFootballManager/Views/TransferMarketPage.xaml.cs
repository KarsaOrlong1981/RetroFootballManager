using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class TransferMarketPage : ContentPage
    {
        private readonly TransferMarketViewModel _viewModel;

        public TransferMarketPage(TransferMarketViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.InitializeAsync();
        }
    }
}
