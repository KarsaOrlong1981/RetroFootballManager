using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class OptionsPage : BaseContentPage
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
