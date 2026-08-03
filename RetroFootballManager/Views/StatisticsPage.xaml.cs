using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class StatisticsPage : BaseContentPage
    {
        private readonly StatisticsViewModel _viewModel;

        public StatisticsPage(StatisticsViewModel viewModel)
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
