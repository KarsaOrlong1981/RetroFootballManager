using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class StaffPage : BaseContentPage
    {
        private readonly StaffViewModel _viewModel;

        public StaffPage(StaffViewModel viewModel)
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
