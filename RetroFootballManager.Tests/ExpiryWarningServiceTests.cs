using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class ExpiryWarningServiceTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private ExpiryWarningService _service = null!;
        private ContractRepository _contractRepo = null!;
        private LoanAgreementRepository _loanRepo = null!;
        private MessageService _messages = null!;
        private Team _team = null!;

        private static readonly DateTime Today = new(2026, 9, 1);

        public ExpiryWarningServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_expiry_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _contractRepo = new ContractRepository(_db);
            _loanRepo = new LoanAgreementRepository(_db);
            _messages = new MessageService(new MessageRepository(_db));
            _service = new ExpiryWarningService(_contractRepo, _loanRepo, _messages);
            _team = TestHelpers.CreateTeam("Verein", baseRating: 60);
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public async Task CheckAsync_WarnsWhenContractWithin30Days()
        {
            var player = _team.Players[0];
            await _contractRepo.SaveAsync(new Contract
            {
                HolderId = player.Id, HolderType = ContractHolderType.Player, TeamId = _team.Id,
                StartDate = Today.AddYears(-1), EndDate = Today.AddDays(20),
            });

            await _service.CheckAsync(_team, Today);

            var inbox = await _messages.GetInboxAsync();
            Assert.Single(inbox);
            Assert.Equal(MessageType.ContractExpiringSoon, inbox[0].Type);
        }

        [Fact]
        public async Task CheckAsync_DoesNotWarnTwiceForSameThreshold()
        {
            var player = _team.Players[0];
            await _contractRepo.SaveAsync(new Contract
            {
                HolderId = player.Id, HolderType = ContractHolderType.Player, TeamId = _team.Id,
                StartDate = Today.AddYears(-1), EndDate = Today.AddDays(20),
            });

            await _service.CheckAsync(_team, Today);
            await _service.CheckAsync(_team, Today.AddDays(1));

            var inbox = await _messages.GetInboxAsync();
            Assert.Single(inbox);
        }

        [Fact]
        public async Task CheckAsync_DoesNotWarnWhenFarFromExpiry()
        {
            var player = _team.Players[0];
            await _contractRepo.SaveAsync(new Contract
            {
                HolderId = player.Id, HolderType = ContractHolderType.Player, TeamId = _team.Id,
                StartDate = Today.AddYears(-1), EndDate = Today.AddYears(1),
            });

            await _service.CheckAsync(_team, Today);

            Assert.Empty(await _messages.GetInboxAsync());
        }

        [Fact]
        public async Task CheckAsync_WarnsWhenLoanExpiringSoon()
        {
            var player = _team.Players[0];
            await _loanRepo.SaveAsync(new LoanAgreement
            {
                PlayerId = player.Id, OriginTeamId = 999, LoanTeamId = _team.Id,
                StartDate = Today.AddMonths(-1), EndDate = Today.AddDays(10), Returned = false,
            });

            await _service.CheckAsync(_team, Today);

            var inbox = await _messages.GetInboxAsync();
            Assert.Single(inbox);
            Assert.Equal(MessageType.LoanExpiringSoon, inbox[0].Type);
        }
    }
}
