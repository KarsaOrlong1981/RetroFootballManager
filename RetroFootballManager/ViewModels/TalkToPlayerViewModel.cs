using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Models;
using RetroFootballManager.Services;
using RetroFootballManager.ViewModels.Messages;

namespace RetroFootballManager.ViewModels
{
    public partial class TalkToPlayerViewModel : BaseViewModel, IQueryAttributable
    {
        private readonly GameSession _session;
        private readonly SaveGameService _saveGame;

        private int _playerId;
        private Team? _team;
        private PlayerStats? _playerStats;

        [ObservableProperty] private Player? _player;
        [ObservableProperty] private PlayerProfile? _profile;
        [ObservableProperty] private string _assessmentText = string.Empty;
        [ObservableProperty] private string _reactionText = string.Empty;
        [ObservableProperty] private bool _wantsToLeaveClub;
        [ObservableProperty] private bool _isGoalKeeper;

        public TalkToPlayerViewModel(IDispatcher dispatcher, GameSession session, SaveGameService saveGame)
            : base(dispatcher)
        {
            _session = session;
            _saveGame = saveGame;
            Title = "Mit Spieler sprechen";
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("playerId", out var value) && int.TryParse(value?.ToString(), out int id))
                _playerId = id;
        }

        public async Task InitializeAsync()
        {
            _team = _session.ManagerTeam;
            var player = _team?.Players.FirstOrDefault(p => p.Id == _playerId);
            if (player is null || _session.State is null)
                return;

            Player = player;
            ReactionText = string.Empty;
            WantsToLeaveClub = player.WantsToLeaveClub;
            IsGoalKeeper = player.Position == Position.Goalkeeper;
            var contract = await _saveGame.GetActivePlayerContractAsync(player.Id, _session.State.CurrentDate);
            var listing = await _saveGame.GetTransferListingForPlayerAsync(player.Id);
            var seasonStats = await _saveGame.GetPlayerSeasonStatsAsync(player.Id, _session.State.Season);
            var careerStats = await _saveGame.GetPlayerCareerStatsAsync(player.Id);
            var competitionStats = await _saveGame.GetPlayerCompetitionBreakdownAsync(player.Id);
            Profile = PlayerProfile.From(player, contract, listing, seasonStats, careerStats, competitionStats);
            _playerStats = seasonStats;
        }

        #region Conversation section

        public string OptionAButtonText => ConversationService.GetConversationText(PlayerConversationOption.EncourageFutureChance);
        public string OptionBButtonText => ConversationService.GetConversationText(PlayerConversationOption.CriticizeUnderperformance);
        public string OptionCButtonText => ConversationService.GetConversationText(PlayerConversationOption.PraiseStrongPerformance);
        public string OptionDButtonText => ConversationService.GetConversationText(PlayerConversationOption.AddressLackOfMatchPractice);
        public string OptionEButtonText => ConversationService.GetConversationText(PlayerConversationOption.ConfirmKeySquadRole);
        public string OptionFButtonText => ConversationService.GetConversationText(PlayerConversationOption.Personal);

        [RelayCommand]
        private async Task ChooseOptionA() => await Talk(PlayerConversationOption.EncourageFutureChance);
        [RelayCommand]
        private async Task ChooseOptionB() => await Talk(PlayerConversationOption.CriticizeUnderperformance);
        [RelayCommand]
        private async Task ChooseOptionC() => await Talk(PlayerConversationOption.PraiseStrongPerformance);
        [RelayCommand]
        private async Task ChooseOptionD() => await Talk(PlayerConversationOption.AddressLackOfMatchPractice);
        [RelayCommand]
        private async Task ChooseOptionE() => await Talk(PlayerConversationOption.ConfirmKeySquadRole);
        [RelayCommand]
        private async Task ChooseOptionF() => await Talk(PlayerConversationOption.Personal);


        private async Task Talk(PlayerConversationOption option)
        {
            if (Player is null || _team is null || _session.State is null)
                return;
            var type = ConversationService.GetTalkType(option);
            int matchFactor = GetMatchFactor(option);
            var result = ConversationService.Talk(Player, type, _session.State, matchFactor, _team.ManagerProfile);
            ReactionText = result.ReactionText;
            WantsToLeaveClub = result.WantsToLeaveClub;
            OnPropertyChanged(nameof(Player));

            if (result.Applied)
            {
                await _saveGame.SaveTeamProgressAsync(_session.State, _team);
                WeakReferenceMessenger.Default.Send(new PlayerProfileChangedMessage(Player.Id));
            }
        }

        private int GetMatchFactor(PlayerConversationOption option)
        {
            int appearances = _playerStats?.Appearances ?? 0;
            double rating = _playerStats?.Rating ?? 0;

            return option switch
            {
                PlayerConversationOption.EncourageFutureChance =>
                    appearances == 0 ? 3 : 1,
                PlayerConversationOption.PraiseStrongPerformance =>
                    (rating > 0 && rating <= 3.0) ? 3 : 1,
                PlayerConversationOption.CriticizeUnderperformance =>
                    (rating >= 4.5) ? 3 : 1,
                PlayerConversationOption.AddressLackOfMatchPractice =>
                    appearances < 5 ? 3 : 2,
                PlayerConversationOption.ConfirmKeySquadRole =>
                    appearances >= 5 ? 3 : 2,

                PlayerConversationOption.Personal => 2,
                _ => 1
            };
        }

        #endregion
    }
}
