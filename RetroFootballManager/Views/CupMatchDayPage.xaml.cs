using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class CupMatchDayPage : ContentPage
    {
        private readonly CupMatchDayViewModel _viewModel;
        private bool _initialized;

        public CupMatchDayPage(CupMatchDayViewModel viewModel)
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
