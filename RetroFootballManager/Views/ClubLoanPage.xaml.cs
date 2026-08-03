using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class ClubLoanPage : BaseContentPage
    {
        private readonly ClubLoanViewModel _viewModel;

        public ClubLoanPage(ClubLoanViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _viewModel.Initialize();
        }
    }
}
