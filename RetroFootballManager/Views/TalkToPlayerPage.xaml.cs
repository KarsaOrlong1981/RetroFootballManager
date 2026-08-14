using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views;

public partial class TalkToPlayerPage : BaseContentPage
{
	private readonly TalkToPlayerViewModel _viewModel;

	public TalkToPlayerPage(TalkToPlayerViewModel viewModel)
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