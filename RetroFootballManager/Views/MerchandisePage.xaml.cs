using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class MerchandisePage : BaseContentPage
    {
        private readonly MerchandiseViewModel _viewModel;

        public MerchandisePage(MerchandiseViewModel viewModel)
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
