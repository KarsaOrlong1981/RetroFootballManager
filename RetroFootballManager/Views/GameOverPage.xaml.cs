using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class GameOverPage : BaseContentPage
    {
        private readonly GameOverViewModel _viewModel;

        public GameOverPage(GameOverViewModel viewModel)
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
