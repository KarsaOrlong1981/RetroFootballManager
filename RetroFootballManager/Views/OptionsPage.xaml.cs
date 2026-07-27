using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class OptionsPage : ContentPage
    {
        private readonly OptionsViewModel _viewModel;

        public OptionsPage(OptionsViewModel viewModel)
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
