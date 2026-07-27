using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class TeamTrainingPage : ContentPage
    {
        private readonly TeamTrainingViewModel _viewModel;
        private bool _initialized;

        public TeamTrainingPage(TeamTrainingViewModel viewModel)
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
