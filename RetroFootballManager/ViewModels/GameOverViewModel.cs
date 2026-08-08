using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public partial class GameOverViewModel : BaseViewModel
    {
        private readonly GameSession _session;
        private readonly INavigationService _navigation;

        public GameOverViewModel(IDispatcher dispatcher, GameSession session, INavigationService navigation)
            : base(dispatcher)
        {
            _session = session;
            _navigation = navigation;
            Title = "Entlassen";
        }

        [ObservableProperty] private string _reasonText = string.Empty;

        public void Initialize()
        {
            var reason = _session.State?.GameOverReason;
            ReasonText = reason switch
            {
                "Vorstand" => "Der Vorstand hat dir das Vertrauen entzogen - du wurdest entlassen.",
                "Fans" => "Die Fans haben sich gegen dich gestellt - du wurdest entlassen.",
                _ => "Du wurdest als Manager entlassen.",
            };
        }

        [RelayCommand]
        private Task BackToStart()
        {
            _session.Clear();
            return _navigation.GoToRootAsync("start");
        }
    }
}
