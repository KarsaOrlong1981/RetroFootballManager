using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class FixturesTablesPage : ContentPage
    {
        private readonly FixturesTablesViewModel _viewModel;

        public FixturesTablesPage(FixturesTablesViewModel viewModel)
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
