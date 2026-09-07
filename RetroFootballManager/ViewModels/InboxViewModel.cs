using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Common;
using RetroFootballManager.Logging;
using RetroFootballManager.Models;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public record InboxMessageRow(int Id, string Title, string Body, string DateText, bool IsRead, bool IsDigest = false);

    // One transfer line inside the digest dialog (see InboxViewModel.OpenDigest) - plain text
    // rows only (no image/Border/button per row), so this list can safely hold far more items
    // per page than the Market/Own-Players/Free-Agents dialogs on TransferMarketPage.
    public record DigestLineRow(string Text, Color Color, bool IsBold);

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
                {
                    bool isDigest = m.Type == MessageType.TransferWindowDigest;
                    string body = isDigest ? BuildDigestSummary(m.Body) : m.Body;
                    Rows.Add(new InboxMessageRow(m.Id, m.Title, body, m.Date.ToString("dd.MM.yyyy"), m.IsRead, isDigest));
                }

                StatusText = messages.Count == 0 ? "Keine Nachrichten." : string.Empty;
            }
            catch (Exception ex)
            {
                Log.Error("Postfach konnte nicht geladen werden.", ex);
                StatusText = "Postfach konnte nicht geladen werden.";
            }
        }

        // A transfer window can produce well over a hundred transfers league-wide - a single
        // Label with that many FormattedText Spans (the old approach) is exactly the kind of
        // "too much complex content realized at once" that crashes WinUI on Windows elsewhere in
        // this app (see TransferMarketPage's crash history) - so the inbox row itself only shows
        // a short summary, with the full list moved into its own paginated dialog (OpenDigest).
        private static string BuildDigestSummary(string rawBody)
        {
            int count = rawBody.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            return count == 0 ? "Keine Transfers." : $"{count} Transfer{(count == 1 ? "" : "s")} - zum Anzeigen tippen.";
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

        // Plain text rows (no image/Border/button), so this can safely hold far more per page
        // than the Market/Own-Players/Free-Agents dialogs - still paginated rather than showing
        // everything at once, since a league-wide transfer window can easily produce 100+ lines.
        private const int DigestPageSize = 40;
        private List<string> _digestLines = [];

        [ObservableProperty] private bool _isDigestDialogOpen;
        [ObservableProperty] private string _digestDialogTitle = string.Empty;
        [ObservableProperty] private int _digestPageIndex;

        public ObservableCollection<DigestLineRow> DigestLinesPage { get; } = [];

        public int DigestPageCount => Math.Max(1, (_digestLines.Count + DigestPageSize - 1) / DigestPageSize);
        public string DigestPageInfo => _digestLines.Count == 0
            ? "Keine Transfers" : $"Seite {DigestPageIndex + 1} von {DigestPageCount} ({_digestLines.Count} Transfers)";
        public bool CanGoToPreviousDigestPage => DigestPageIndex > 0;
        public bool CanGoToNextDigestPage => DigestPageIndex + 1 < DigestPageCount;

        [RelayCommand]
        private void OpenDigest(int messageId)
        {
            if (!_messagesById.TryGetValue(messageId, out var message))
                return;

            _digestLines = message.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
            DigestDialogTitle = message.Title;
            DigestPageIndex = 0;
            UpdateDigestPage();
            IsDigestDialogOpen = true;
        }

        [RelayCommand]
        private void CloseDigest() => IsDigestDialogOpen = false;

        private void UpdateDigestPage()
        {
            DigestLinesPage.Clear();
            foreach (var line in _digestLines.Skip(DigestPageIndex * DigestPageSize).Take(DigestPageSize))
            {
                bool isOwn = line.StartsWith('★');
                DigestLinesPage.Add(new DigestLineRow(isOwn ? line[1..] : line, isOwn ? OwnTransferColor : OtherTransferColor, isOwn));
            }
            OnPropertyChanged(nameof(DigestPageCount));
            OnPropertyChanged(nameof(DigestPageInfo));
            OnPropertyChanged(nameof(CanGoToPreviousDigestPage));
            OnPropertyChanged(nameof(CanGoToNextDigestPage));
        }

        [RelayCommand]
        private void NextDigestPage()
        {
            if (!CanGoToNextDigestPage) return;
            DigestPageIndex++;
            UpdateDigestPage();
        }

        [RelayCommand]
        private void PreviousDigestPage()
        {
            if (!CanGoToPreviousDigestPage) return;
            DigestPageIndex--;
            UpdateDigestPage();
        }
    }
}
