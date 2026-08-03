using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class TrophyCasePage : BaseContentPage
    {
        private readonly TrophyCaseViewModel _viewModel;

        public TrophyCasePage(TrophyCaseViewModel viewModel)
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
