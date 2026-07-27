using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class MatchDayPage : ContentPage
    {
        private readonly MatchDayViewModel _viewModel;
        private bool _initialized;

        public MatchDayPage(MatchDayViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_initialized)
                return;

            _initialized = true;
            await _viewModel.InitializeAsync();
        }
    }
}
