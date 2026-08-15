using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views
{
    public partial class ManagerCreationPage : BaseContentPage
    {
        public ManagerCreationPage(ManagerCreationViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
