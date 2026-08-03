using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class StartPage : BaseContentPage
    {
        private readonly StartViewModel _viewModel;

        public StartPage(StartViewModel viewModel)
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
