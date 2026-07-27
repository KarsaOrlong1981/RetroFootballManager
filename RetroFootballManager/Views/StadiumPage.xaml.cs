using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class StadiumPage : ContentPage
    {
        private readonly StadiumViewModel _viewModel;

        public StadiumPage(StadiumViewModel viewModel)
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
