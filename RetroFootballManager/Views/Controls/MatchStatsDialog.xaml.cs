using System.Windows.Input;
using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views.Controls
{
    // Shared live/full-time match-stats popup, used by MatchDay/CupMatchDay/FriendlyMatchDay -
    // same pattern as PlayerProfileDialog.
    public partial class MatchStatsDialog : ContentView
    {
        public static readonly BindableProperty IsOpenProperty =
            BindableProperty.Create(nameof(IsOpen), typeof(bool), typeof(MatchStatsDialog), false);

        public static readonly BindableProperty HomeStatsProperty =
            BindableProperty.Create(nameof(HomeStats), typeof(MatchStatsDisplay), typeof(MatchStatsDialog));

        public static readonly BindableProperty AwayStatsProperty =
            BindableProperty.Create(nameof(AwayStats), typeof(MatchStatsDisplay), typeof(MatchStatsDialog));

        public static readonly BindableProperty CloseCommandProperty =
            BindableProperty.Create(nameof(CloseCommand), typeof(ICommand), typeof(MatchStatsDialog));

        public MatchStatsDialog()
        {
            InitializeComponent();
        }

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public MatchStatsDisplay? HomeStats
        {
            get => (MatchStatsDisplay?)GetValue(HomeStatsProperty);
            set => SetValue(HomeStatsProperty, value);
        }

        public MatchStatsDisplay? AwayStats
        {
            get => (MatchStatsDisplay?)GetValue(AwayStatsProperty);
            set => SetValue(AwayStatsProperty, value);
        }

        public ICommand? CloseCommand
        {
            get => (ICommand?)GetValue(CloseCommandProperty);
            set => SetValue(CloseCommandProperty, value);
        }
    }
}
