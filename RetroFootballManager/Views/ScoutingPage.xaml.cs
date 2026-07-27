using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class ScoutingPage : ContentPage
    {
        private readonly ScoutingViewModel _viewModel;

        public ScoutingPage(ScoutingViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _ = _viewModel.InitializeAsync();
        }
    }
}
