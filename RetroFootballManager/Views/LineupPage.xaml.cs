using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class LineupPage : ContentPage
    {
        private readonly LineupViewModel _viewModel;
        private bool _initialized;

        public LineupPage(LineupViewModel viewModel)
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
