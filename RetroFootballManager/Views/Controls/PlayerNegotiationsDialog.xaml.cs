namespace RetroFootballManager.Views.Controls;

// Purely a view now - all state/logic lives in the shared NegotiationDialogViewModel
// (RetroFootballManager.ViewModels), set as this control's BindingContext by whichever page
// hosts it (e.g. <controls:PlayerNegotiationsDialog BindingContext="{Binding Negotiation}" />).
public partial class PlayerNegotiationsDialog : ContentView
{
    public PlayerNegotiationsDialog()
	{
		InitializeComponent();
	}
}
