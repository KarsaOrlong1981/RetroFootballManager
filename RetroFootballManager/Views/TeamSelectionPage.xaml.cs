using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class TeamSelectionPage : ContentPage
    {
        private readonly TeamSelectionViewModel _viewModel;
        private bool _initialized;

        public TeamSelectionPage(TeamSelectionViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (_initialized)
                return;

            _initialized = true;
            _viewModel.Initialize();
        }
    }
}
