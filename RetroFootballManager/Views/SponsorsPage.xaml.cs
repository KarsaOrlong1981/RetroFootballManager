using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class SponsorsPage : ContentPage
    {
        private readonly SponsorsViewModel _viewModel;

        public SponsorsPage(SponsorsViewModel viewModel)
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
