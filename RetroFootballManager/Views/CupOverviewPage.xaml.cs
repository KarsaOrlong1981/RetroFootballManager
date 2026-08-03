using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class CupOverviewPage : BaseContentPage
    {
        private readonly CupOverviewViewModel _viewModel;

        public CupOverviewPage(CupOverviewViewModel viewModel)
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
