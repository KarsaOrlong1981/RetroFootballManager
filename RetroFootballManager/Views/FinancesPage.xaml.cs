using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class FinancesPage : ContentPage
    {
        private readonly FinancesViewModel _viewModel;

        public FinancesPage(FinancesViewModel viewModel)
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
