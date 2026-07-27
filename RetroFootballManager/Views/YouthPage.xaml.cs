using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class YouthPage : ContentPage
    {
        private readonly YouthViewModel _viewModel;
        private bool _initialized;

        public YouthPage(YouthViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (_initialized)
                return;
            _initialized = true;
            _viewModel.Initialize();
        }
    }
}
