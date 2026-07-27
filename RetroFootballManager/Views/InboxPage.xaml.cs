using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class InboxPage : ContentPage
    {
        private readonly InboxViewModel _viewModel;

        public InboxPage(InboxViewModel viewModel)
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
