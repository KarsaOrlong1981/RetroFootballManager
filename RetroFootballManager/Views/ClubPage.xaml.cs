using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class ClubPage : BaseContentPage
    {
        private readonly ClubViewModel _viewModel;

        public ClubPage(ClubViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.InitializeAsync();
        }
    }
}
