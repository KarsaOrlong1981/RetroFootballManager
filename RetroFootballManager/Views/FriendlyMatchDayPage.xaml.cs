using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class FriendlyMatchDayPage : ContentPage
    {
        private readonly FriendlyMatchDayViewModel _viewModel;
        private bool _initialized;

        public FriendlyMatchDayPage(FriendlyMatchDayViewModel viewModel)
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
