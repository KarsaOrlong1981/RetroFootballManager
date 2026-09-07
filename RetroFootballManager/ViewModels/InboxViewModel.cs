using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Common;
using RetroFootballManager.Logging;
using RetroFootballManager.Models;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public record InboxMessageRow(int Id, string Title, string Body, string DateText, bool IsRead, FormattedString? BodyFormatted = null)
    {
        public bool HasFormattedBody => BodyFormatted is not null;
    }

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
                    Rows.Add(new InboxMessageRow(
                        m.Id, m.Title, m.Body, m.Date.ToString("dd.MM.yyyy"), m.IsRead,
                        m.Type == MessageType.TransferWindowDigest ? BuildTransferDigestFormatting(m.Body) : null));

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

        // Own-team lines in a TransferWindowDigest body are prefixed with "★" (see
        // TransferWindowDigestService) - turned into a highlighted color here since Inbox
        // messages have no other way to carry per-line formatting. Hex values match
        // RfmAccentTeal/RfmTextMuted (Resources/Styles/Colors.xaml) - a plain C# ViewModel
        // can't reach XAML StaticResources, same reasoning as the match ticker's hardcoded
        // team colors in MatchDayViewModel.
        private static readonly Color OwnTransferColor = Color.FromArgb("#14B8A6");
        private static readonly Color OtherTransferColor = Color.FromArgb("#8FA3B8");

        private static FormattedString BuildTransferDigestFormatting(string body)
        {
            var formatted = new FormattedString();
            var lines = body.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                bool isOwn = lines[i].StartsWith('★');
                string text = isOwn ? lines[i][1..] : lines[i];
                formatted.Spans.Add(new Span
                {
                    Text = i < lines.Length - 1 ? text + "\n" : text,
                    TextColor = isOwn ? OwnTransferColor : OtherTransferColor,
                    FontAttributes = isOwn ? FontAttributes.Bold : FontAttributes.None,
                });
            }
            return formatted;
        }
    }
}
