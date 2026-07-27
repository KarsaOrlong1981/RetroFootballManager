using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Common;
using RetroFootballManager.Logging;
using RetroFootballManager.Models;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public record InboxMessageRow(int Id, string Title, string Body, string DateText, bool IsRead);

    public partial class InboxViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<InboxViewModel>();

        private readonly MessageService _messages;
        private readonly INavigationService _navigation;
        private Dictionary<int, Message> _messagesById = new();

        public InboxViewModel(IDispatcher dispatcher, MessageService messages, INavigationService navigation)
            : base(dispatcher)
        {
            _messages = messages;
            _navigation = navigation;
            Title = "Postfach";
        }

        public ObservableCollection<InboxMessageRow> Rows { get; } = [];

        [ObservableProperty] private string _statusText = string.Empty;

        public async Task InitializeAsync()
        {
            Rows.Clear();
            try
            {
                var messages = await _messages.GetInboxAsync();
                _messagesById = messages.ToDictionary(m => m.Id);
                foreach (var m in messages)
                    Rows.Add(new InboxMessageRow(m.Id, m.Title, m.Body, m.Date.ToString("dd.MM.yyyy"), m.IsRead));

                StatusText = messages.Count == 0 ? "Keine Nachrichten." : string.Empty;
            }
            catch (Exception ex)
            {
                Log.Error("Postfach konnte nicht geladen werden.", ex);
                StatusText = "Postfach konnte nicht geladen werden.";
            }
        }

        [RelayCommand]
        private async Task MarkRead(int messageId)
        {
            if (!_messagesById.TryGetValue(messageId, out var message) || message.IsRead)
                return;

            await _messages.MarkReadAsync(message);
            int index = Rows.ToList().FindIndex(r => r.Id == messageId);
            if (index >= 0)
                Rows[index] = Rows[index] with { IsRead = true };
        }

        [RelayCommand]
        private async Task Delete(int messageId)
        {
            if (!_messagesById.TryGetValue(messageId, out var message))
                return;

            await _messages.DeleteAsync(message);
            _messagesById.Remove(messageId);
            int index = Rows.ToList().FindIndex(r => r.Id == messageId);
            if (index >= 0)
                Rows.RemoveAt(index);

            StatusText = Rows.Count == 0 ? "Keine Nachrichten." : string.Empty;
        }

        [RelayCommand]
        private async Task Back() => await _navigation.GoBackAsync();
    }
}
