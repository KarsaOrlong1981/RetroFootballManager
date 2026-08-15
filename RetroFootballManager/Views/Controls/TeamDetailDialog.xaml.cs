using System.Windows.Input;
using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views.Controls
{
    // Team detail popup shown when tapping a row in the club overview (own or foreign team) -
    // same pattern as PlayerProfileDialog/MatchStatsDialog.
    public partial class TeamDetailDialog : ContentView
    {
        public static readonly BindableProperty IsOpenProperty =
            BindableProperty.Create(nameof(IsOpen), typeof(bool), typeof(TeamDetailDialog), false);

        public static readonly BindableProperty DetailProperty =
            BindableProperty.Create(nameof(Detail), typeof(TeamDetail), typeof(TeamDetailDialog));

        public static readonly BindableProperty CloseCommandProperty =
            BindableProperty.Create(nameof(CloseCommand), typeof(ICommand), typeof(TeamDetailDialog));

        public static readonly BindableProperty ShowManagerCommandProperty =
            BindableProperty.Create(nameof(ShowManagerCommand), typeof(ICommand), typeof(TeamDetailDialog));

        public TeamDetailDialog()
        {
            InitializeComponent();
        }

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public TeamDetail? Detail
        {
            get => (TeamDetail?)GetValue(DetailProperty);
            set => SetValue(DetailProperty, value);
        }

        public ICommand? CloseCommand
        {
            get => (ICommand?)GetValue(CloseCommandProperty);
            set => SetValue(CloseCommandProperty, value);
        }

        public ICommand? ShowManagerCommand
        {
            get => (ICommand?)GetValue(ShowManagerCommandProperty);
            set => SetValue(ShowManagerCommandProperty, value);
        }
    }
}
