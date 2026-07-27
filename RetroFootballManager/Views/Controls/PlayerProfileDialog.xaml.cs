using System.Windows.Input;
using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views.Controls
{
    // Shared player-detail popup used by every page that shows a player profile
    // (Lineup, Youth, Training, Statistics, MatchDay/CupMatchDay/FriendlyMatchDay,
    // TransferMarket, Scouting). Centralizes the dialog markup so attributes/fields
    // only need to be added once (in PlayerProfile/PlayerAttributeSummary) instead
    // of in every page's copy-pasted overlay.
    public partial class PlayerProfileDialog : ContentView
    {
        public static readonly BindableProperty IsOpenProperty =
            BindableProperty.Create(nameof(IsOpen), typeof(bool), typeof(PlayerProfileDialog), false);

        public static readonly BindableProperty TitleProperty =
            BindableProperty.Create(nameof(Title), typeof(string), typeof(PlayerProfileDialog), string.Empty);

        public static readonly BindableProperty ProfileProperty =
            BindableProperty.Create(nameof(Profile), typeof(PlayerProfile), typeof(PlayerProfileDialog));

        public static readonly BindableProperty CloseCommandProperty =
            BindableProperty.Create(nameof(CloseCommand), typeof(ICommand), typeof(PlayerProfileDialog));

        public static readonly BindableProperty IsLockedProperty =
            BindableProperty.Create(nameof(IsLocked), typeof(bool), typeof(PlayerProfileDialog), false,
                propertyChanged: OnIsLockedChanged);

        public static readonly BindableProperty LockedStatusTextProperty =
            BindableProperty.Create(nameof(LockedStatusText), typeof(string), typeof(PlayerProfileDialog), string.Empty);

        public static readonly BindableProperty IsLockedBusyProperty =
            BindableProperty.Create(nameof(IsLockedBusy), typeof(bool), typeof(PlayerProfileDialog), false);

        public static readonly BindableProperty UnlockCommandProperty =
            BindableProperty.Create(nameof(UnlockCommand), typeof(ICommand), typeof(PlayerProfileDialog));

        public static readonly BindableProperty ExtraContentProperty =
            BindableProperty.Create(nameof(ExtraContent), typeof(View), typeof(PlayerProfileDialog),
                propertyChanged: OnExtraContentChanged);

        public PlayerProfileDialog()
        {
            InitializeComponent();
        }

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public PlayerProfile? Profile
        {
            get => (PlayerProfile?)GetValue(ProfileProperty);
            set => SetValue(ProfileProperty, value);
        }

        public ICommand? CloseCommand
        {
            get => (ICommand?)GetValue(CloseCommandProperty);
            set => SetValue(CloseCommandProperty, value);
        }

        // When true, shows the "not scouted yet" prompt instead of the profile body
        // (used by Statistics/TransferMarket/Scouting; unused pages simply never set it).
        public bool IsLocked
        {
            get => (bool)GetValue(IsLockedProperty);
            set => SetValue(IsLockedProperty, value);
        }

        public string LockedStatusText
        {
            get => (string)GetValue(LockedStatusTextProperty);
            set => SetValue(LockedStatusTextProperty, value);
        }

        public bool IsLockedBusy
        {
            get => (bool)GetValue(IsLockedBusyProperty);
            set => SetValue(IsLockedBusyProperty, value);
        }

        public ICommand? UnlockCommand
        {
            get => (ICommand?)GetValue(UnlockCommandProperty);
            set => SetValue(UnlockCommandProperty, value);
        }

        // Optional page-specific row rendered above the close button
        // (e.g. Training's "Vertrag verlängern" button).
        public View? ExtraContent
        {
            get => (View?)GetValue(ExtraContentProperty);
            set => SetValue(ExtraContentProperty, value);
        }

        private static void OnIsLockedChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var dialog = (PlayerProfileDialog)bindable;
            var locked = (bool)newValue;
            dialog.LockedContent.IsVisible = locked;
            dialog.BodyScroll.IsVisible = !locked;
        }

        private static void OnExtraContentChanged(BindableObject bindable, object oldValue, object newValue)
        {
            ((PlayerProfileDialog)bindable).ExtraContentHost.Content = newValue as View;
        }
    }
}
