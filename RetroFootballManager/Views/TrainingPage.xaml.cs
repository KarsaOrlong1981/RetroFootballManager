using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class TrainingPage : BaseContentPage
    {
        private readonly TrainingViewModel _viewModel;
        private bool _initialized;

        public TrainingPage(TrainingViewModel viewModel)
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
